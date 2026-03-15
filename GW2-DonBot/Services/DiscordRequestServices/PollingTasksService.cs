using Discord.WebSocket;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class PollingTasksService(
    IEntityService entityService,
    ILogger<PollingTasksService> logger,
    IHttpClientFactory httpClientFactory)
    : IPollingTasksService
{
    public async Task PollingRoles(DiscordSocketClient discordClient)
    {
        var accounts = await entityService.Account.GetAllAsync();
        var guilds = await entityService.Guild.GetAllAsync();
        var guildWarsAccounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.GuildWarsApiKey != null);

        accounts = accounts.Where(s => guildWarsAccounts.Select(gw => gw.DiscordId).Contains(s.DiscordId)).ToList();

        var guildWars2Data = new ConcurrentDictionary<long, List<GuildWars2AccountDataModel>>();

        var maxDegreeOfParallelism = Environment.ProcessorCount;
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

        if (accounts.Count > 0 && (double)guildWars2Data.Count / accounts.Count < 0.5)
        {
            logger.LogWarning("GW2 API appears to be down ({SuccessfulFetches}/{TotalAccounts} accounts fetched). Skipping role changes.", guildWars2Data.Count, accounts.Count);
            return;
        }

        await entityService.GuildWarsAccount.UpdateRangeAsync(guildWarsAccounts);
        foreach (var clientGuild in discordClient.Guilds)
        {
            var guildConfiguration = guilds.FirstOrDefault(g => g.GuildId == (long)clientGuild.Id);

            if (guildConfiguration == null)
            {
                continue;
            }

            await clientGuild.DownloadUsersAsync();
            await HandleGuildUsers(clientGuild, guildConfiguration, guildWars2Data, accounts, guildWarsAccounts);
        }
    }

    private async Task<List<GuildWars2AccountDataModel>> FetchAccountData(List<GuildWarsAccount> guildWarsAccounts)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(20);

        var guildWarsAccountData = new List<GuildWars2AccountDataModel>();

        foreach (var guildWarsAccount in guildWarsAccounts)
        {
            GuildWars2AccountDataModel? accountData = null;
            var success = false;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    logger.LogInformation("Handling {guildWarsAccountName} - Attempt {attempt}",
                        guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);

                    var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={guildWarsAccount.GuildWarsApiKey}");
                    if (response.IsSuccessStatusCode)
                    {
                        var stringData = await response.Content.ReadAsStringAsync();
                        accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(stringData) ?? new GuildWars2AccountDataModel();

                        guildWarsAccount.FailedApiPullCount = 0;
                        guildWarsAccount.World = Convert.ToInt32(accountData.World);
                        guildWarsAccount.GuildWarsGuilds = string.Join(",", accountData.Guilds);

                        success = true;
                        break;
                    }

                    logger.LogWarning("Attempt {attempt} failed for {guildWarsAccountName}: {statusCode}",
                        attempt, guildWarsAccount.GuildWarsAccountName?.Trim(), response.StatusCode);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "FAILED Handling {guildWarsAccountName} on Attempt {attempt}",
                        guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);
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

        return guildWarsAccountData;
    }

    private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, ConcurrentDictionary<long, List<GuildWars2AccountDataModel>> guildWars2Data, List<Account> accounts, List<GuildWarsAccount> gwAccounts)
    {
        var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
        var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
        var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

        var guildId = guildConfiguration.Gw2GuildMemberRoleId;
        var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds?.Split(',').ToList() ?? [];

        if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
        {
            return;
        }

        foreach (var user in guild.Users)
        {
            logger.LogInformation("HANDLING {userDisplayName}", user.DisplayName);

            var playerAccount = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
            var playerGwAccounts = gwAccounts.Where(s => s.DiscordId == (long)user.Id).ToList();

            if (playerAccount == null || !playerGwAccounts.Any() || playerGwAccounts.All(s => string.IsNullOrEmpty(s.GuildWarsApiKey)))
            {
                await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value);

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

        // Clear API keys for accounts with 48+ failed pulls (likely left the server)
        var expiredAccounts = gwAccounts.Where(s => s.FailedApiPullCount >= 48).ToList();
        foreach (var invalidGuildWarsAccount in expiredAccounts)
        {
            logger.LogInformation("Guild Wars 2 account {invalidGuildWarsAccountGuildWarsAccountName} is no longer valid and has expired.", invalidGuildWarsAccount.GuildWarsAccountName);
            invalidGuildWarsAccount.GuildWarsApiKey = null;
        }

        if (expiredAccounts.Any())
        {
            await entityService.GuildWarsAccount.UpdateRangeAsync(expiredAccounts);
        }
    }

    private async Task RemoveAllRolesFromUser(SocketGuildUser user, long primaryRoleId, long secondaryRoleId, long verifiedRoleId)
    {
        var roles = user.Roles.Select(s => s.Id).ToList();

        var removedRoles = false;

        if (roles.Contains((ulong)primaryRoleId))
        {
            await user.RemoveRoleAsync((ulong)primaryRoleId);
            logger.LogInformation("Removing Primary Role");
            removedRoles = true;
        }

        if (roles.Contains((ulong)secondaryRoleId))
        {
            await user.RemoveRoleAsync((ulong)secondaryRoleId);
            logger.LogInformation("Removing Secondary Role");
            removedRoles = true;
        }

        if (roles.Contains((ulong)verifiedRoleId))
        {
            await user.RemoveRoleAsync((ulong)verifiedRoleId);
            logger.LogInformation("Removing Verified Role");
            removedRoles = true;
        }

        if (removedRoles)
        {
            logger.LogWarning("{userDisplayName} did not have the required GW2 details, removing all roles.", user.DisplayName);
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
            logger.LogInformation("Removing Primary Role");
        }
        else if (!roles.Contains((ulong)primaryRoleId) && inPrimaryGuild)
        {
            await user.AddRoleAsync((ulong)primaryRoleId);
            logger.LogInformation("Adding Primary Role");
        }
    }

    private async Task HandleSecondaryGuildRoles(SocketGuildUser user, bool inSecondaryGuild, long secondaryRoleId, List<ulong> roles)
    {
        if (roles.Contains((ulong)secondaryRoleId) && !inSecondaryGuild)
        {
            await user.RemoveRoleAsync((ulong)secondaryRoleId);
            logger.LogInformation("Removing Secondary Role");
        }
        else if (!roles.Contains((ulong)secondaryRoleId) && inSecondaryGuild)
        {
            await user.AddRoleAsync((ulong)secondaryRoleId);
            logger.LogInformation("Adding Secondary Role");
        }
    }

    private async Task HandleVerifiedRole(SocketGuildUser user, long verifiedRoleId, List<ulong> roles)
    {
        if (!roles.Contains((ulong)verifiedRoleId))
        {
            await user.AddRoleAsync((ulong)verifiedRoleId);
            logger.LogInformation("Adding Verified Role");
        }
    }
}