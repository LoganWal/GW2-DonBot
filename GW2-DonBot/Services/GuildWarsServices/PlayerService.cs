using DonBot.Core.Models.Entities;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices;

public sealed class PlayerService(IEntityService entityService) : IPlayerService
{
    public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, bool someAllFights = true) =>
        EliteInsightPlayerMapper.GetGw2Players(data, fightPhase, someAllFights);

    public async Task SetPlayerPoints(EliteInsightDataModel fightEliteInsightDataModel)
    {
        if (fightEliteInsightDataModel.FightEliteInsightDataModel.Players == null)
        {
            return;
        }

        var accounts = await entityService.Account.GetAllAsync();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
        accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();
        if (!accounts.Any())
        {
            return;
        }

        var fightPhase = fightEliteInsightDataModel.FightEliteInsightDataModel.Phases?.FirstOrDefault() ?? new ArcDpsPhase();

        var gw2Players = GetGw2Players(fightEliteInsightDataModel, fightPhase);

        var secondsOfFight = 0;
        if (TimeSpan.TryParse(fightEliteInsightDataModel.FightEliteInsightDataModel.Phases?.FirstOrDefault()?.EncounterDuration, out var duration))
        {
            secondsOfFight = (int)duration.TotalSeconds;
        }

        var currentDateTimeUtc = DateTime.UtcNow;
        foreach (var account in accounts)
        {
            account.PreviousPoints = account.Points;
        }

        await entityService.Account.UpdateRangeAsync(accounts);

        var accountsToUpdate = new List<Account>();

        foreach (var player in gw2Players)
        {
            var gw2Account = gw2Accounts.FirstOrDefault(a => string.Equals(a.GuildWarsAccountName, player.AccountName, StringComparison.OrdinalIgnoreCase));
            if (gw2Account == null)
            {
                continue;
            }

            var account = await entityService.Account.GetFirstOrDefaultAsync(s => s.DiscordId == gw2Account.DiscordId);
            if (account == null)
            {
                continue;
            }

            var totalPoints = 0d;

            totalPoints += Math.Min(Convert.ToDouble(player.Damage) / 50000d, 10);
            totalPoints += Math.Min(Convert.ToDouble(player.Cleanses) / 100d, 5);
            totalPoints += Math.Min(Convert.ToDouble(player.Strips) / 30d, 3);
            totalPoints += Math.Min(Convert.ToDouble(player.StabOnGroup) * (secondsOfFight < 20d ? 1d : secondsOfFight / 20d), 6);
            totalPoints += Math.Min(Convert.ToDouble(player.Healing) / 50000d, 4);
            totalPoints += Math.Min(Convert.ToDouble(player.BarrierGenerated) / 40000d, 3);

            totalPoints = Math.Max(4, Math.Min(12, totalPoints));

            account.Points += Convert.ToDecimal(totalPoints);
            account.AvailablePoints += Convert.ToDecimal(totalPoints);
            account.LastWvwLogDateTime = currentDateTimeUtc;

            accountsToUpdate.Add(account);
        }

        if (accountsToUpdate.Any())
        {
            await entityService.Account.UpdateRangeAsync(accountsToUpdate);
        }
    }
}
