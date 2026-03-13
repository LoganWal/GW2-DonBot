using Discord.WebSocket;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class PointsCommandsService(IEntityService entityService) : IPointsCommandsService
{
    public async Task PointsCommandExecuted(SocketSlashCommand command)
    {
        var accounts = await entityService.Account.GetAllAsync();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
        accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();

        var account = accounts.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

        int? rank = account != null
            ? accounts.OrderByDescending(o => o.Points).ToList().FindIndex(m => (ulong)m.DiscordId == command.User.Id) + 1
            : null;

        var output = account != null
            ? $"You have earned {Math.Round(account.Points)} points.{Environment.NewLine}You have {Math.Round(account.AvailablePoints)} Available Points for spending.{Environment.NewLine}Current Rank: {rank}"
            : "Unable to find account, have you verified?";

        await command.FollowupAsync(output, ephemeral: true);
    }

    public async Task PointsCommandExecuted(SocketMessageComponent command)
    {
        await command.DeferAsync(ephemeral: true);

        var accounts = await entityService.Account.GetAllAsync();
        var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
        accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();

        var account = accounts.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

        int? rank = account != null
            ? accounts.OrderByDescending(o => o.Points).ToList().FindIndex(m => (ulong)m.DiscordId == command.User.Id) + 1
            : null;

        var output = account != null
            ? $"You have earned {Math.Round(account.Points)} points.{Environment.NewLine}You have {Math.Round(account.AvailablePoints)} Available Points for spending.{Environment.NewLine}Current Rank: {rank}"
            : "Unable to find account, have you verified?";

        await command.FollowupAsync(output, ephemeral: true);
    }
}