using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Models.Entities;
using Models.GW2Api;
using Newtonsoft.Json;
using Services.LogGenerationServices;

namespace Services.DiscordRequestServices
{
    public class PollingTasksService : IPollingTasksService
    {
        private readonly DatabaseContext _databaseContext;
        private readonly IMessageGenerationService _messageGenerationService;

        public PollingTasksService(IDatabaseContext databaseContext, IMessageGenerationService messageGenerationService)
        {
            _databaseContext = databaseContext.GetDatabaseContext();
            _messageGenerationService = messageGenerationService;
        }

        public async Task PollingRoles(DiscordSocketClient discordClient)
        {
            var accounts = await _databaseContext.Account.Where(acc => acc.Gw2ApiKey != null).ToListAsync();
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guildWars2Data = new Dictionary<long, GW2AccountDataModel?>();

            foreach (var account in accounts)
            {
                Console.WriteLine($"=== FETCHING {account.Gw2AccountName.Trim()} ===");

                var accountData = await FetchAccountData(account);

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

                //await RemoveRolesFromNonServerAccounts(clientGuild, accounts);

                await HandleGuildUsers(clientGuild, guildConfiguration, guildWars2Data);

                await GenerateWvWPlayerReport(guildConfiguration, clientGuild);
            }
        }

        private async Task<GW2AccountDataModel?> FetchAccountData(Account account)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            try
            {
                var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={account.Gw2ApiKey}");
                if (response.IsSuccessStatusCode)
                {
                    var stringData = await response.Content.ReadAsStringAsync();
                    var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                    account.FailedApiPullCount = 0;
                    account.World = Convert.ToInt32(accountData.World);
                    account.Guilds = string.Join(",", accountData.Guilds);

                    return accountData;
                }

                account.FailedApiPullCount = account.FailedApiPullCount == null ? 1 : account.FailedApiPullCount + 1;

                return null;
            }
            catch (Exception ex)
            {
                account.FailedApiPullCount = account.FailedApiPullCount == null ? 1 : account.FailedApiPullCount++;

                Console.WriteLine($"=== FAILED Handling {account.Gw2AccountName.Trim()} ===");
                Console.WriteLine(ex.Message);

                return null;
            }
        }

        private async Task RemoveRolesFromNonServerAccounts(SocketGuild guild, List<Account> accounts)
        {
            var guildUserIds = guild.Users.Select(s => (long)s.Id);
            var nonServerAccounts = accounts.Where(s => !guildUserIds.Contains(s.DiscordId)).ToList();

            foreach (var nonServerAccount in nonServerAccounts)
            {
                nonServerAccount.Gw2ApiKey = null;
                _databaseContext.Update(nonServerAccount);

                await _databaseContext.SaveChangesAsync();
            }
        }

        private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, Dictionary<long, GW2AccountDataModel?> guildWars2Data)
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
                Console.WriteLine($"=== HANDLING {user.DisplayName} ===");

                var account = _databaseContext.Account.FirstOrDefault(f => f.DiscordId == (long)user.Id);

                if (account == null)
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);

                    Console.WriteLine($"Failed to get {user.DisplayName} GW2 details, removing all roles.");

                    continue;
                }

                if (account.FailedApiPullCount >= 48)
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);

                    account.Gw2ApiKey = null;
                    _databaseContext.Update(account);

                    await _databaseContext.SaveChangesAsync();

                    var dmChannel = await user.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync($"Heyo {user.DisplayName}, I failed to pull your GW2 data using your API key throughout the day and have removed your roles for the server {guild.Name}, to get back the roles just use /verify in any channel in {guild.Name}.");

                    continue;
                }

                if (guildWars2Data.TryGetValue(account.DiscordId, out var accountData))
                {
                    await HandleUserRoles(user, accountData, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value, guildId, secondaryGuildIds);

                    continue;
                }

                await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);

                Console.WriteLine($"API KEY no longer valid for {user.DisplayName} GW2 details, removing all roles.");
            }
        }

        private async Task GenerateWvWPlayerReport(Guild guildConfiguration, SocketGuild clientGuild)
        {
            try
            {
                if (!guildConfiguration.WvwPlayerActivityReportChannelId.HasValue) {
                    Console.WriteLine("Failed to find WvW Player Activity Report Channel Id for guild {0}", clientGuild.Name);
                    return;
                }

                if (guildConfiguration.WvwPlayerActivityReportWebhook != null && clientGuild.GetChannel((ulong)guildConfiguration.WvwPlayerActivityReportChannelId) is SocketTextChannel playerChannel)
                {
                    var messages = await playerChannel.GetMessagesAsync(100).FlattenAsync();
                    await playerChannel.DeleteMessagesAsync(messages);
                    var playerReportMessage = await _messageGenerationService.GenerateWvWPlayerReport();
                    
                    var playerReport = new DiscordWebhookClient(guildConfiguration.WvwPlayerActivityReportWebhook);
                    await playerReport.SendMessageAsync(text: "", username: "GW2-DonBot", avatarUrl: "https://i.imgur.com/tQ4LD6H.png", embeds: new[] { playerReportMessage });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task RemoveAllRolesFromUser(SocketGuildUser user, long primaryRoleId, long secondaryRoleId, long verifiedRoleId)
        {
            var roles = user.Roles.Select(s => s.Id).ToList();

            if (roles.Contains((ulong)primaryRoleId))
            {
                await user.RemoveRoleAsync((ulong)primaryRoleId);
                Console.WriteLine(" - Removing Primary Role");
            }

            if (roles.Contains((ulong)secondaryRoleId))
            {
                await user.RemoveRoleAsync((ulong)secondaryRoleId);
                Console.WriteLine(" - Removing Secondary Role");
            }

            if (roles.Contains((ulong)verifiedRoleId))
            {
                await user.RemoveRoleAsync((ulong)verifiedRoleId);
                Console.WriteLine(" + Removing Verified Role");
            }
        }

        private async Task HandleUserRoles(SocketGuildUser user, GW2AccountDataModel? accountData, long primaryRoleId, long secondaryRoleId, long verifiedRoleId, string? guildId, List<string> secondaryGuildIds)
        {
            var inPrimary = accountData?.Guilds.Contains(guildId) ?? false;
            var inSecondary = secondaryGuildIds.Any(guild => accountData?.Guilds.Contains(guild) ?? false);

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
                Console.WriteLine(" - Removing Primary Role");
            }
            else if (!roles.Contains((ulong)primaryRoleId) && inPrimaryGuild)
            {
                await user.AddRoleAsync((ulong)primaryRoleId);
                Console.WriteLine(" + Adding Primary Role");
            }
        }

        private async Task HandleSecondaryGuildRoles(SocketGuildUser user, bool inSecondaryGuild, long secondaryRoleId, List<ulong> roles)
        {
            if (roles.Contains((ulong)secondaryRoleId) && !inSecondaryGuild)
            {
                await user.RemoveRoleAsync((ulong)secondaryRoleId);
                Console.WriteLine(" - Removing Secondary Role");
            }
            else if (!roles.Contains((ulong)secondaryRoleId) && inSecondaryGuild)
            {
                await user.AddRoleAsync((ulong)secondaryRoleId);
                Console.WriteLine(" + Adding Secondary Role");
            }
        }

        private async Task HandleVerifiedRole(SocketGuildUser user, long verifiedRoleId, List<ulong> roles)
        {
            if (!roles.Contains((ulong)verifiedRoleId))
            {
                await user.AddRoleAsync((ulong)verifiedRoleId);
                Console.WriteLine(" + Adding Verified Role");
            }
        }
    }
}