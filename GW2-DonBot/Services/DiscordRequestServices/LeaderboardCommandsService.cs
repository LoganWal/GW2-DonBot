using Discord.WebSocket;
using DonBot.Services.DatabaseServices;
using DonBot.Services.GuildWarsServices.MessageGeneration;

namespace DonBot.Services.DiscordRequestServices;

public sealed class LeaderboardCommandsService(
    IEntityService entityService,
    IWeeklyLeaderboardService weeklyLeaderboardService) : ILeaderboardCommandsService
{
    public async Task MyRankCommandExecuted(SocketSlashCommand command)
    {
        if (command.GuildId == null)
        {
            await command.FollowupAsync("This command must be used within a Discord server.", ephemeral: true);
            return;
        }

        var guild = await entityService.Guild.GetFirstOrDefaultAsync(g => g.GuildId == (long)command.GuildId);
        if (guild == null)
        {
            await command.FollowupAsync("Cannot find server configuration.", ephemeral: true);
            return;
        }

        if (!guild.WvwLeaderboardEnabled && !guild.PveLeaderboardEnabled)
        {
            await command.FollowupAsync("No leaderboards are currently enabled for this server.", ephemeral: true);
            return;
        }

        var gw2Accounts = await entityService.GuildWarsAccount.GetWhereAsync(a => a.DiscordId == (long)command.User.Id);
        var accountNames = gw2Accounts.Select(a => a.GuildWarsAccountName).Where(n => n != null).Cast<string>().ToList();
        if (accountNames.Count == 0)
        {
            await command.FollowupAsync("You need to verify your GW2 account first using `/gw2_verify`.", ephemeral: true);
            return;
        }

        var embed = await weeklyLeaderboardService.GetPlayerRanks(guild, accountNames);
        if (embed == null)
        {
            await command.FollowupAsync("No leaderboard data found for the past 7 days.", ephemeral: true);
            return;
        }

        await command.FollowupAsync(embeds: [embed], ephemeral: true);
    }
}
