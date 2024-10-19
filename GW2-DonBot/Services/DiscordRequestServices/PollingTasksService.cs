using Controller.Discord;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Models.Entities;
using Models.GW2Api;
using Newtonsoft.Json;
using Services.LogGenerationServices;

namespace Services.DiscordRequestServices
{
    public class PollingTasksService : IPollingTasksService
    {
        private readonly ILogger<PollingTasksService> _logger;

        private readonly IMessageGenerationService _messageGenerationService;

        private readonly DatabaseContext _databaseContext;

        public PollingTasksService(ILogger<PollingTasksService> logger, IMessageGenerationService messageGenerationService, DatabaseContext databaseContext)
        {
            _logger = logger;
            _messageGenerationService = messageGenerationService;
            _databaseContext = databaseContext;
        }

        public async Task PollingRoles(DiscordSocketClient discordClient)
        {
            var accounts = await _databaseContext.Account.ToListAsync();
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guildWarsAccounts = await _databaseContext.GuildWarsAccount.ToListAsync();

            accounts = accounts.Where(s => guildWarsAccounts.Select(gw => gw.DiscordId).Contains(s.DiscordId)).ToList();

            var guildWars2Data = new Dictionary<long, List<Gw2AccountDataModel>>();

            foreach (var account in accounts)
            {
                var guildWars2Accounts = guildWarsAccounts.Where(s => s.DiscordId == account.DiscordId).ToList();
                var accountData = await FetchAccountData(guildWars2Accounts);

                guildWars2Data.Add(account.DiscordId, accountData);

                _databaseContext.Update(account);
            }

            await _databaseContext.SaveChangesAsync();

            foreach (var clientGuild in discordClient.Guilds)
            {
                var guildConfiguration = guilds.FirstOrDefault(g => g.GuildId == (long)clientGuild.Id);

                if (guildConfiguration == null)
                {
                    continue;
                }

                await HandleGuildUsers(clientGuild, guildConfiguration, guildWars2Data);
                await GenerateWvWPlayerReport(guildConfiguration, clientGuild);
            }
        }

        private async Task<List<Gw2AccountDataModel>> FetchAccountData(List<GuildWarsAccount> guildWarsAccounts)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            var guildWarsAccountData = new List<Gw2AccountDataModel>();

            foreach (var guildWarsAccount in guildWarsAccounts)
            {
                Gw2AccountDataModel? accountData = null;
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
                            accountData = JsonConvert.DeserializeObject<Gw2AccountDataModel>(stringData) ?? new Gw2AccountDataModel();

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

                    await Task.Delay(1000); // Wait 1 second before retrying
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

            return guildWarsAccountData;
        }

        private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, Dictionary<long, List<Gw2AccountDataModel>> guildWars2Data)
        {
            var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
            var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
            var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

            var guildId = guildConfiguration.Gw2GuildMemberRoleId;
            var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds?.Split(',').ToList() ?? new List<string>();

            var accounts = _databaseContext.Account.ToList();

            if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
            {
                return;
            }

            foreach (var user in guild.Users)
            {
                _logger.LogInformation("HANDLING {userDisplayName}", user.DisplayName);

                var playerAccount = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
                var playerGwAccounts = _databaseContext.GuildWarsAccount.Where(s => s.DiscordId == (long)user.Id);

                if (playerAccount == null || !playerGwAccounts.Any() || playerGwAccounts.All(s => string.IsNullOrEmpty(s.GuildWarsApiKey)))
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);

                    _logger.LogWarning("Failed to get {userDisplayName} GW2 details, removing all roles.", user.DisplayName);

                    continue;
                }

                if (playerGwAccounts.All(s => s.FailedApiPullCount >= 48))
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);
                    foreach (var guildWarsAccount in playerGwAccounts)
                    {
                        guildWarsAccount.GuildWarsApiKey = null;
                    }

                    _databaseContext.UpdateRange(playerGwAccounts);
                    await _databaseContext.SaveChangesAsync();

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

        private async Task HandleUserRoles(SocketGuildUser user, List<Gw2AccountDataModel> accountData, long primaryRoleId, long secondaryRoleId, long verifiedRoleId, string? guildId, List<string> secondaryGuildIds)
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