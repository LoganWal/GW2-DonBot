using Discord;
using Discord.WebSocket;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using DonBot.Services.LogGenerationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace DonBot.Services.DiscordRequestServices
{
    public class PollingTasksService : IPollingTasksService
    {
        private readonly ILogger<PollingTasksService> _logger;

        private readonly IMessageGenerationService _messageGenerationService;

        private readonly DatabaseContext _databaseContext;

        private readonly IServiceProvider _serviceProvider;

        public PollingTasksService(ILogger<PollingTasksService> logger, IMessageGenerationService messageGenerationService, DatabaseContext databaseContext, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _messageGenerationService = messageGenerationService;
            _databaseContext = databaseContext;
            _serviceProvider = serviceProvider;
        }

        public async Task PollingRoles(DiscordSocketClient discordClient)
        {
            var accounts = await _databaseContext.Account.ToListAsync();
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guildWarsAccounts = await _databaseContext.GuildWarsAccount.Where(s => s.GuildWarsApiKey != null).ToListAsync();

            accounts = accounts.Where(s => guildWarsAccounts.Select(gw => gw.DiscordId).Contains(s.DiscordId)).ToList();

            var guildWars2Data = new ConcurrentDictionary<long, List<GuildWars2AccountDataModel>>();

            var maxDegreeOfParallelism = Environment.ProcessorCount; // Limit to available logical processors
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = accounts.Select(async account =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var guildWars2Accounts = guildWarsAccounts.Where(s => s.DiscordId == account.DiscordId).ToList();
                    var accountData = await FetchAccountData(guildWars2Accounts);

                    guildWars2Data[account.DiscordId] = accountData;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            foreach (var clientGuild in discordClient.Guilds)
            {
                var guildConfiguration = guilds.FirstOrDefault(g => g.GuildId == (long)clientGuild.Id);

                if (guildConfiguration == null)
                {
                    continue;
                }
                await clientGuild.DownloadUsersAsync();
                await HandleGuildUsers(clientGuild, guildConfiguration, guildWars2Data, accounts, guildWarsAccounts);
                await GenerateWvWPlayerReport(guildConfiguration, clientGuild);
            }
        }

        private async Task<List<GuildWars2AccountDataModel>> FetchAccountData(List<GuildWarsAccount> guildWarsAccounts)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            var guildWarsAccountData = new List<GuildWars2AccountDataModel>();

            foreach (var guildWarsAccount in guildWarsAccounts)
            {
                GuildWars2AccountDataModel? accountData = null;
                var success = false;

                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        _logger.LogInformation("Handling {guildWarsAccountName} - Attempt {attempt}", guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);

                        var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={guildWarsAccount.GuildWarsApiKey}");
                        if (response.IsSuccessStatusCode)
                        {
                            var stringData = await response.Content.ReadAsStringAsync();
                            accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(stringData) ?? new GuildWars2AccountDataModel();

                            guildWarsAccount.FailedApiPullCount = 0;
                            guildWarsAccount.World = Convert.ToInt32(accountData.World);
                            guildWarsAccount.GuildWarsGuilds = string.Join(",", accountData.Guilds);

                            success = true;
                            break; // Exit the retry loop if successful
                        }

                        _logger.LogWarning("Attempt {attempt} failed for {guildWarsAccountName}: {statusCode}", attempt, guildWarsAccount.GuildWarsAccountName?.Trim(), response.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "FAILED Handling {guildWarsAccountName} on Attempt {attempt}", guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);
                    }

                    await Task.Delay(250);
                }

                if (!success)
                {
                    guildWarsAccount.FailedApiPullCount += 1;
                }
                else if (accountData != null)
                {
                    guildWarsAccountData.Add(accountData);
                }
            }

            using var scope = _serviceProvider.CreateScope(); // Create a scoped service provider
            var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>(); // Resolve DbContext
            dbContext.UpdateRange(guildWarsAccounts);
            await dbContext.SaveChangesAsync();

            return guildWarsAccountData;
        }

        private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, ConcurrentDictionary<long, List<GuildWars2AccountDataModel>> guildWars2Data, List<Account> accounts, List<GuildWarsAccount> gwAccounts)
        {
            var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
            var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
            var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

            var guildId = guildConfiguration.Gw2GuildMemberRoleId;
            var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds?.Split(',').ToList() ?? new List<string>();

            if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
            {
                return;
            }

            foreach (var user in guild.Users)
            {
                _logger.LogInformation("HANDLING {userDisplayName}", user.DisplayName);

                var playerAccount = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
                var playerGwAccounts = gwAccounts.Where(s => s.DiscordId == (long)user.Id).ToList();
                var invalidAccounts = playerGwAccounts.Where(s => s.FailedApiPullCount >= 48).ToList();

                foreach (var invalidGuildWarsAccount in invalidAccounts)
                {
                    _logger.LogWarning("Guild Wars 2 account {invalidGuildWarsAccountGuildWarsAccountName} is no longer valid .", invalidGuildWarsAccount.GuildWarsAccountName);

                    invalidGuildWarsAccount.GuildWarsApiKey = null;
                    _databaseContext.Update(invalidGuildWarsAccount);
                }

                if (invalidAccounts.Any())
                {
                    await _databaseContext.SaveChangesAsync();
                }

                if (playerAccount == null || !playerGwAccounts.Any() || playerGwAccounts.All(s => string.IsNullOrEmpty(s.GuildWarsApiKey)))
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);
                    _logger.LogWarning("Failed to get {userDisplayName} GW2 details, removing all roles.", user.DisplayName);

                    continue;
                }

                if (playerGwAccounts.All(s => s.FailedApiPullCount >= 48))
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);
                    continue;
                }

                if (!guildWars2Data.TryGetValue(playerAccount.DiscordId, out var accountData))
                {
                    continue;
                }

                await HandleUserRoles(user, accountData, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value, guildId, secondaryGuildIds);
            }
        }

        private async Task GenerateWvWPlayerReport(Guild guildConfiguration, SocketGuild clientGuild)
        {
            try
            {
                if (!guildConfiguration.WvwPlayerActivityReportChannelId.HasValue) {
                    _logger.LogError("Failed to find WvW Player Activity Report Channel Id for guild {clientGuildName}", clientGuild.Name);
                    return;
                }

                if (clientGuild.GetChannel((ulong)guildConfiguration.WvwPlayerActivityReportChannelId) is SocketTextChannel playerActivityReportChannel)
                {
                    var messages = await playerActivityReportChannel.GetMessagesAsync(100).FlattenAsync();
                    await playerActivityReportChannel.DeleteMessagesAsync(messages);
                    var playerReportMessage = await _messageGenerationService.GenerateWvWPlayerReport(guildConfiguration);
                    
                    await playerActivityReportChannel.SendMessageAsync(embeds: new[] { playerReportMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, "Failed to generate WvW player report.");
            }
        }

        private async Task RemoveAllRolesFromUser(SocketGuildUser user, long primaryRoleId, long secondaryRoleId, long verifiedRoleId)
        {
            var roles = user.Roles.Select(s => s.Id).ToList();

            if (roles.Contains((ulong)primaryRoleId))
            {
                await user.RemoveRoleAsync((ulong)primaryRoleId);
                _logger.LogInformation("Removing Primary Role");
            }

            if (roles.Contains((ulong)secondaryRoleId))
            {
                await user.RemoveRoleAsync((ulong)secondaryRoleId);
                _logger.LogInformation("Removing Secondary Role");
            }

            if (roles.Contains((ulong)verifiedRoleId))
            {
                await user.RemoveRoleAsync((ulong)verifiedRoleId);
                _logger.LogInformation("Removing Verified Role");
            }
        }

        private async Task HandleUserRoles(SocketGuildUser user, List<GuildWars2AccountDataModel> accountData, long primaryRoleId, long secondaryRoleId, long verifiedRoleId, string? guildId, List<string> secondaryGuildIds)
        {
            var inPrimary = accountData.SelectMany(s => s.Guilds).Contains(guildId);
            var inSecondary = secondaryGuildIds.Any(guild => accountData.SelectMany(s => s.Guilds).Contains(guild));

            var roles = user.Roles.Select(s => s.Id).ToList();

            await HandlePrimaryGuildRole(user, inPrimary, primaryRoleId, roles);

            await HandleSecondaryGuildRoles(user, inSecondary, secondaryRoleId, roles);

            await HandleVerifiedRole(user, verifiedRoleId, roles);
        }

        
        private async Task HandlePrimaryGuildRole(SocketGuildUser user, bool inPrimaryGuild, long primaryRoleId, List<ulong> roles)
        {
            if (roles.Contains((ulong)primaryRoleId) && !inPrimaryGuild)
            {
                await user.RemoveRoleAsync((ulong)primaryRoleId);
                _logger.LogInformation("Removing Primary Role");
            }
            else if (!roles.Contains((ulong)primaryRoleId) && inPrimaryGuild)
            {
                await user.AddRoleAsync((ulong)primaryRoleId);
                _logger.LogInformation("Adding Primary Role");
            }
        }

        private async Task HandleSecondaryGuildRoles(SocketGuildUser user, bool inSecondaryGuild, long secondaryRoleId, List<ulong> roles)
        {
            if (roles.Contains((ulong)secondaryRoleId) && !inSecondaryGuild)
            {
                await user.RemoveRoleAsync((ulong)secondaryRoleId);
                _logger.LogInformation("Removing Secondary Role");
            }
            else if (!roles.Contains((ulong)secondaryRoleId) && inSecondaryGuild)
            {
                await user.AddRoleAsync((ulong)secondaryRoleId);
                _logger.LogInformation("Adding Secondary Role");
            }
        }

        private async Task HandleVerifiedRole(SocketGuildUser user, long verifiedRoleId, List<ulong> roles)
        {
            if (!roles.Contains((ulong)verifiedRoleId))
            {
                await user.AddRoleAsync((ulong)verifiedRoleId);
                _logger.LogInformation("Adding Verified Role");
            }
        }
    }
}