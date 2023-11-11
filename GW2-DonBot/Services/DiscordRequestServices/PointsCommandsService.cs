using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Services.DiscordRequestServices
{
    public class PointsCommandsService : IPointsCommandsService
    {
        private readonly DatabaseContext _databaseContext;

        public PointsCommandsService(IDatabaseContext databaseContext)
        {
            _databaseContext = databaseContext.GetDatabaseContext();
        }

        public async Task PointsCommandExecuted(SocketSlashCommand command)
        {
            await command.DeferAsync(ephemeral: true);

            // Fetch accounts with non-null Gw2ApiKey
            var accounts = await _databaseContext.Account
                .Where(acc => acc.Gw2ApiKey != null)
                .ToListAsync();

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

            await command.ModifyOriginalResponseAsync(message => message.Content = output);
        }
    }
}