using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Services.DeadlockServices;

namespace DonBot.Services.DiscordRequestServices
{
    public class DeadlockCommandService : IDeadlockCommandService
    {
        private readonly DatabaseContext _databaseContext;

        private readonly IDeadlockApiService _deadlockApiService;

        public DeadlockCommandService(DatabaseContext databaseContext, IDeadlockApiService deadlockApiService)
        {
            _databaseContext = databaseContext;
            _deadlockApiService = deadlockApiService;
        }

        public async Task GetMmr(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            var steamAccounts = _databaseContext.SteamAccount.Where(g => g.DiscordId == (long)command.User.Id);
            if (steamAccounts.Any())
            {
                var userMmr = string.Empty;
                foreach (var steamAccount in steamAccounts)
                {
                    var result = await _deadlockApiService.GetDeadlockRank(steamAccount.SteamId3);
                    if (result.PlayerScore > 0)
                    {
                        userMmr += $"Account {steamAccount.SteamId64}, MMR: {result.PlayerScore}, Leaderboard: {result.LeaderboardRank}{Environment.NewLine}";
                    }
                }

                if (userMmr.Length > 0)
                {
                    await command.FollowupAsync($"```{userMmr}```", ephemeral: true);
                }
                else
                {
                    await command.FollowupAsync("No recorded stats", ephemeral: true);
                }
            }
            else
            {
                await command.FollowupAsync("Verify your steam account to use this command!", ephemeral: true);
            }
        }

        public async Task GetMmrHistory(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            var steamAccounts = _databaseContext.SteamAccount.Where(g => g.DiscordId == (long)command.User.Id);
            if (steamAccounts.Any())
            {
                var mmrOverview = "Account Id         Date        MMR                                                         \n";
                var hasResults = false;
                foreach (var steamAccount in steamAccounts)
                {
                    var result = await _deadlockApiService.GetDeadlockRankHistory(steamAccount.SteamId3);
                    result = result.Where(s => s.PlayerScore > 0).OrderByDescending(s => s.MatchStartTime).Take(5).ToList();

                    if (!result.Any())
                    {
                        continue;
                    }

                    hasResults = true;
                    mmrOverview = result.Aggregate(mmrOverview, (current, deadlockRankHistory) => current + $"{steamAccount.SteamId64,-13}{string.Empty,2}{deadlockRankHistory.MatchStartTime,-8:d}{string.Empty,2}{deadlockRankHistory.PlayerScore,-5}\n");
                }

                if (hasResults)
                {
                    await command.FollowupAsync($"```{mmrOverview}```", ephemeral: true);
                }
                else
                {
                    await command.FollowupAsync("No recorded stats", ephemeral: true);
                }
            }
            else
            {
                await command.FollowupAsync("Verify your steam account to use this command!", ephemeral: true);
            }
        }

        public async Task GetMatchHistory(SocketSlashCommand command, DiscordSocketClient discordClient)
        {
            await command.FollowupAsync("Work in progress", ephemeral: true);
        }
    }
}
