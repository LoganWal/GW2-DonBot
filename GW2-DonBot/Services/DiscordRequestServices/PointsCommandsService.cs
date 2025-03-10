using Discord.WebSocket;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices
{
    public class PointsCommandsService(IEntityService entityService) : IPointsCommandsService
    {
        public async Task PointsCommandExecuted(SocketSlashCommand command)
        {
            // Fetch accounts with non-null Gw2ApiKey
            var accounts = await entityService.Account.GetAllAsync();
            var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
            accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();

            // Find the account of the user who executed the command
            var account = accounts.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

            // Calculate rank if account exists
            int? rank = account != null
                ? accounts.OrderByDescending(o => o.Points).ToList().FindIndex(m => (ulong)m.DiscordId == command.User.Id) + 1
                : null;

            // Prepare output message
            var output = account != null
                ? $"You have earned {Math.Round(account.Points)} points.{Environment.NewLine}You have {Math.Round(account.AvailablePoints)} Available Points for spending.{Environment.NewLine}Current Rank: {rank}"
                : "Unable to find account, have you verified?";

            await command.FollowupAsync(output, ephemeral: true);
        }

        public async Task PointsCommandExecuted(SocketMessageComponent command)
        {
            await command.DeferAsync(ephemeral: true);

            // Fetch accounts with non-null Gw2ApiKey
            var accounts = await entityService.Account.GetAllAsync();
            var gw2Accounts = await entityService.GuildWarsAccount.GetAllAsync();
            accounts = accounts.Where(s => gw2Accounts.Any(acc => acc.DiscordId == s.DiscordId)).ToList();

            // Find the account of the user who executed the command
            var account = accounts.FirstOrDefault(m => (ulong)m.DiscordId == command.User.Id);

            // Calculate rank if account exists
            int? rank = account != null
                ? accounts.OrderByDescending(o => o.Points).ToList().FindIndex(m => (ulong)m.DiscordId == command.User.Id) + 1
                : null;

            // Prepare output message
            var output = account != null
                ? $"You have earned {Math.Round(account.Points)} points.{Environment.NewLine}You have {Math.Round(account.AvailablePoints)} Available Points for spending.{Environment.NewLine}Current Rank: {rank}"
                : "Unable to find account, have you verified?";

            await command.FollowupAsync(output, ephemeral: true);
        }
    }
}