using Discord;
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

        public PollingTasksService(DatabaseContext databaseContext, IMessageGenerationService messageGenerationService)
        {
            _databaseContext = databaseContext;
            _messageGenerationService = messageGenerationService;
        }

        public async Task PollingRoles(DiscordSocketClient discordClient)
        {
            var accounts = await _databaseContext.Account.ToListAsync();
            var guilds = await _databaseContext.Guild.ToListAsync();
            var guildWarsAccounts = await _databaseContext.GuildWarsAccount.ToListAsync();

            accounts = accounts.Where(s => guildWarsAccounts.Select(gw => gw.DiscordId).Contains(s.DiscordId)).ToList();

            var guildWars2Data = new Dictionary<long, List<GW2AccountDataModel>>();

            foreach (var account in accounts)
            {
                var guildWars2Accounts = guildWarsAccounts.Where(s => s.DiscordId == account.DiscordId).ToList();
                var accountData = await FetchAccountData(guildWars2Accounts);

                guildWars2Data.Add(account.DiscordId, accountData);

                _databaseContext.Update(account);
            }

            _databaseContext.SaveChanges();

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

        private async Task<List<GW2AccountDataModel>> FetchAccountData(List<GuildWarsAccount> guildWarsAccounts)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            var guildWarsAccountData = new List<GW2AccountDataModel>();

            foreach (var guildWarsAccount in guildWarsAccounts)
            {
                try
                {
                    Console.WriteLine($"=== Handling {guildWarsAccount.GuildWarsAccountName?.Trim()} ===");

                    var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={guildWarsAccount.GuildWarsApiKey}");
                    if (response.IsSuccessStatusCode)
                    {
                        var stringData = await response.Content.ReadAsStringAsync();
                        var accountData = JsonConvert.DeserializeObject<GW2AccountDataModel>(stringData) ?? new GW2AccountDataModel();

                        guildWarsAccount.FailedApiPullCount = 0;
                        guildWarsAccount.World = Convert.ToInt32(accountData.World);
                        guildWarsAccount.GuildWarsGuilds = string.Join(",", accountData.Guilds);

                        guildWarsAccountData.Add(accountData);
                        continue;
                    }

                    guildWarsAccount.FailedApiPullCount += 1;
                }
                catch (Exception ex)
                {
                    guildWarsAccount.FailedApiPullCount += 1;

                    Console.WriteLine($"=== FAILED Handling {guildWarsAccount.GuildWarsAccountName?.Trim()} ===");
                    Console.WriteLine(ex.Message);
                }
            }

            return guildWarsAccountData;
        }

        private async Task RemoveRolesFromNonServerAccounts(SocketGuild guild, List<Account> accounts)
        {
            var guildUserIds = guild.Users.Select(s => (long)s.Id);
            var nonServerAccounts = accounts.Where(s => !guildUserIds.Contains(s.DiscordId)).ToList();

            foreach (var nonServerAccount in nonServerAccounts)
            {
                //nonServerAccount.Gw2ApiKey = null;
                _databaseContext.Update(nonServerAccount);
                _databaseContext.SaveChanges();
            }
        }

        private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, Dictionary<long, List<GW2AccountDataModel>> guildWars2Data)
        {
            var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
            var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
            var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

            var guildId = guildConfiguration.Gw2GuildMemberRoleId;
            var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds?.Split(',').ToList() ?? new List<string>();

            var accounts = _databaseContext.Account.ToList();
            var gwAccounts = _databaseContext.GuildWarsAccount.ToList();

            if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
            {
                return;
            }

            foreach (var user in guild.Users)
            {
                Console.WriteLine($"=== HANDLING {user.DisplayName} ===");

                var playerAccount = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
                var playerGwAccounts = _databaseContext.GuildWarsAccount.Where(s => s.DiscordId == (long)user.Id);

                if (playerAccount == null || !playerGwAccounts.Any() || playerGwAccounts.All(s => string.IsNullOrEmpty(s.GuildWarsApiKey)))
                {
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);

                    Console.WriteLine($"Failed to get {user.DisplayName} GW2 details, removing all roles.");

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
                    _databaseContext.SaveChanges();

                    var dmChannel = await user.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync($"Heyo {user.DisplayName}, I failed to pull your GW2 data using your API key throughout the day and have removed your roles for the server {guild.Name}, to get back the roles just use /verify in any channel in {guild.Name}.");

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
                    Console.WriteLine("Failed to find WvW Player Activity Report Channel Id for guild {0}", clientGuild.Name);
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
                Console.WriteLine(" - Removing Verified Role");
            }
        }

        private async Task HandleUserRoles(SocketGuildUser user, List<GW2AccountDataModel> accountData, long primaryRoleId, long secondaryRoleId, long verifiedRoleId, string? guildId, List<string> secondaryGuildIds)
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