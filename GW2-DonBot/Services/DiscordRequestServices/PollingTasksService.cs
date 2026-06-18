using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;
using Discord.Net;
using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Services.DatabaseServices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.DiscordRequestServices;

public sealed class PollingTasksService(
    IEntityService entityService,
    ILogger<PollingTasksService> logger,
    IHttpClientFactory httpClientFactory)
    : IPollingTasksService, IDisposable
{
    // Leave room under GW2's 600/min cap for verification and manual API calls.
    private const int Gw2RequestsPerSecond = 5;
    private const int UnknownMemberDiscordCode = 10007;
    private const int InvalidKeyBadRequestClearThreshold = 3;
    private const int SystemicKeyClearMinimumCount = 3;
    private const double SystemicKeyClearRatio = 0.5;
    private const string GuildWars2BuildUrl = "https://api.guildwars2.com/v2/build";

    private readonly TokenBucketRateLimiter _gw2RateLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = Gw2RequestsPerSecond,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 1000,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokensPerPeriod = Gw2RequestsPerSecond,
        AutoReplenishment = true
    });

    // Lets tests skip retry and rate-limit waits.
    internal Func<TimeSpan, Task> DelayAsync { get; init; } = Task.Delay;

    public void Dispose() => _gw2RateLimiter.Dispose();

    public async Task PollingRoles(DiscordSocketClient discordClient)
    {
        var accounts = await entityService.Account.GetAllAsync();
        var guilds = await entityService.Guild.GetAllAsync();
        var guildWarsAccounts = await entityService.GuildWarsAccount.GetAllAsync();
        var guildWarsAccountsWithKeys = guildWarsAccounts.Where(s => !string.IsNullOrEmpty(s.GuildWarsApiKey)).ToList();

        var accountIdsWithKeys = guildWarsAccountsWithKeys.Select(gw => gw.DiscordId).ToHashSet();
        var accountsWithKeys = accounts.Where(s => accountIdsWithKeys.Contains(s.DiscordId)).ToList();

        if (accountsWithKeys.Count > 0 && !await IsGuildWars2ApiAvailable())
        {
            logger.LogWarning("GW2 API health check failed. Skipping role polling so keys and Discord roles are left unchanged.");
            return;
        }

        var originalApiKeys = SnapshotApiKeys(guildWarsAccountsWithKeys);
        var guildWars2Data = new ConcurrentDictionary<long, List<GuildWars2AccountDataModel>>();

        var tasks = accountsWithKeys.Select(async account =>
        {
            var guildWars2Accounts = guildWarsAccountsWithKeys.Where(s => s.DiscordId == account.DiscordId).ToList();
            var accountData = await FetchAccountData(guildWars2Accounts);
            // Omit failed fetches so transient API failures do not strip existing roles.
            if (accountData.Count > 0)
            {
                guildWars2Data[account.DiscordId] = accountData;
            }
        });

        await Task.WhenAll(tasks);

        var clearedApiKeysCount = CountClearedApiKeys(originalApiKeys, guildWarsAccountsWithKeys);
        if (IsPotentialSystemicKeyClear(clearedApiKeysCount, originalApiKeys.Count))
        {
            RestoreApiKeys(originalApiKeys, guildWarsAccountsWithKeys);
            logger.LogError("GW2 API auth failures would clear {ClearedKeys}/{TotalKeys} stored keys. Skipping key persistence and role changes to avoid mass de-verification.",
                clearedApiKeysCount, originalApiKeys.Count);
            return;
        }

        var remainingAccountsWithKeysCount = CountAccountsWithRemainingKeys(accountsWithKeys, guildWarsAccountsWithKeys);

        if (remainingAccountsWithKeysCount > 0 && (double)guildWars2Data.Count / remainingAccountsWithKeysCount < 0.5)
        {
            RestoreApiKeys(originalApiKeys, guildWarsAccountsWithKeys);
            logger.LogWarning("GW2 API appears to be down ({SuccessfulFetches}/{TotalAccounts} accounts fetched). Skipping key persistence and role changes.",
                guildWars2Data.Count, remainingAccountsWithKeysCount);
            return;
        }

        await entityService.GuildWarsAccount.UpdateRangeAsync(guildWarsAccountsWithKeys);

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

    internal async Task<bool> IsGuildWars2ApiAvailable()
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            using var response = await httpClient.GetAsync(GuildWars2BuildUrl);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            logger.LogWarning("GW2 API health check returned {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GW2 API health check failed");
        }

        return false;
    }

    internal async Task<List<GuildWars2AccountDataModel>> FetchAccountData(List<GuildWarsAccount> guildWarsAccounts)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(20);

        var guildWarsAccountData = new List<GuildWars2AccountDataModel>();

        foreach (var guildWarsAccount in guildWarsAccounts)
        {
            GuildWars2AccountDataModel? accountData = null;
            var success = false;
            var invalidKeyBadRequestCount = 0;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var lease = await _gw2RateLimiter.AcquireAsync(permitCount: 1);
                    if (!lease.IsAcquired)
                    {
                        logger.LogWarning("Rate limiter queue full for {GuildWarsAccountName}, skipping", guildWarsAccount.GuildWarsAccountName?.Trim());
                        break;
                    }

                    logger.LogInformation("Handling {GuildWarsAccountName} - Attempt {Attempt}",
                        guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);

                    var response = await httpClient.GetAsync($"https://api.guildwars2.com/v2/account/?access_token={guildWarsAccount.GuildWarsApiKey}");

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
                        logger.LogWarning("GW2 API rate limit hit for {GuildWarsAccountName}, waiting {RetryAfterSeconds}s",
                            guildWarsAccount.GuildWarsAccountName?.Trim(), retryAfter.TotalSeconds);
                        await DelayAsync(retryAfter);
                        continue;
                    }

                    if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        // Auth failures are permanent, so clear the key and strip roles this cycle.
                        logger.LogWarning("GW2 API key for {GuildWarsAccountName} rejected with {StatusCode}: clearing key",
                            guildWarsAccount.GuildWarsAccountName?.Trim(), response.StatusCode);
                        guildWarsAccount.GuildWarsApiKey = null;
                        break;
                    }

                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        var body = response.Content == null
                            ? string.Empty
                            : await response.Content.ReadAsStringAsync();

                        if (IsInvalidApiKeyBadRequest(body))
                        {
                            invalidKeyBadRequestCount++;
                            if (invalidKeyBadRequestCount >= InvalidKeyBadRequestClearThreshold)
                            {
                                logger.LogWarning("GW2 API key for {GuildWarsAccountName} returned invalid-key BadRequest {InvalidKeyBadRequestCount} times: clearing key",
                                    guildWarsAccount.GuildWarsAccountName?.Trim(), invalidKeyBadRequestCount);
                                guildWarsAccount.GuildWarsApiKey = null;
                                break;
                            }

                            logger.LogWarning("Attempt {Attempt} failed for {GuildWarsAccountName}: invalid-key BadRequest ({InvalidKeyBadRequestCount}/{InvalidKeyBadRequestClearThreshold})",
                                attempt, guildWarsAccount.GuildWarsAccountName?.Trim(), invalidKeyBadRequestCount, InvalidKeyBadRequestClearThreshold);
                        }
                        else
                        {
                            logger.LogWarning("Attempt {Attempt} failed for {GuildWarsAccountName}: BadRequest did not confirm an invalid key",
                                attempt, guildWarsAccount.GuildWarsAccountName?.Trim());
                        }
                    }
                    else if (response.IsSuccessStatusCode)
                    {
                        var stringData = await response.Content.ReadAsStringAsync();
                        accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(stringData) ?? new GuildWars2AccountDataModel();

                        guildWarsAccount.World = Convert.ToInt32(accountData.World);
                        guildWarsAccount.GuildWarsGuilds = string.Join(",", accountData.Guilds);

                        success = true;
                        break;
                    }
                    else
                    {
                        logger.LogWarning("Attempt {Attempt} failed for {GuildWarsAccountName}: {StatusCode}",
                            attempt, guildWarsAccount.GuildWarsAccountName?.Trim(), response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "FAILED handling {GuildWarsAccountName} on attempt {Attempt}",
                        guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);
                }

                // Jitter avoids synchronized retry waves during GW2 API outages.
                var backoffSeconds = Math.Pow(2, attempt - 1);
                var jitter = Random.Shared.NextDouble() * backoffSeconds * 0.25;
                await DelayAsync(TimeSpan.FromSeconds(backoffSeconds + jitter));
            }

            if (success && accountData != null)
            {
                guildWarsAccountData.Add(accountData);
            }
        }

        return guildWarsAccountData;
    }

    internal static int CountAccountsWithRemainingKeys(IEnumerable<Account> accountsWithKeys, IEnumerable<GuildWarsAccount> fetchedGuildWarsAccounts)
    {
        var remainingAccountIdsWithKeys = fetchedGuildWarsAccounts
            .Where(gw => !string.IsNullOrEmpty(gw.GuildWarsApiKey))
            .Select(gw => gw.DiscordId)
            .ToHashSet();

        return accountsWithKeys.Count(account => remainingAccountIdsWithKeys.Contains(account.DiscordId));
    }

    internal static Dictionary<Guid, string> SnapshotApiKeys(IEnumerable<GuildWarsAccount> guildWarsAccounts) =>
        guildWarsAccounts
            .Where(gw => !string.IsNullOrEmpty(gw.GuildWarsApiKey))
            .ToDictionary(gw => gw.GuildWarsAccountId, gw => gw.GuildWarsApiKey!);

    internal static int CountClearedApiKeys(IReadOnlyDictionary<Guid, string> originalApiKeys, IEnumerable<GuildWarsAccount> fetchedGuildWarsAccounts) =>
        fetchedGuildWarsAccounts.Count(gw =>
            originalApiKeys.ContainsKey(gw.GuildWarsAccountId)
            && string.IsNullOrEmpty(gw.GuildWarsApiKey));

    internal static bool IsPotentialSystemicKeyClear(int clearedKeyCount, int originalKeyCount) =>
        originalKeyCount > 0
        && (clearedKeyCount == originalKeyCount
            || clearedKeyCount >= SystemicKeyClearMinimumCount
            && (double)clearedKeyCount / originalKeyCount >= SystemicKeyClearRatio);

    internal static void RestoreApiKeys(IReadOnlyDictionary<Guid, string> originalApiKeys, IEnumerable<GuildWarsAccount> fetchedGuildWarsAccounts)
    {
        foreach (var guildWarsAccount in fetchedGuildWarsAccounts)
        {
            if (originalApiKeys.TryGetValue(guildWarsAccount.GuildWarsAccountId, out var originalApiKey))
            {
                guildWarsAccount.GuildWarsApiKey = originalApiKey;
            }
        }
    }

    internal static bool IsInvalidApiKeyBadRequest(string responseBody) =>
        responseBody.Contains("invalid key", StringComparison.OrdinalIgnoreCase)
        || responseBody.Contains("invalid access token", StringComparison.OrdinalIgnoreCase);

    private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, ConcurrentDictionary<long, List<GuildWars2AccountDataModel>> guildWars2Data, List<Account> accounts, List<GuildWarsAccount> gwAccounts)
    {
        var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
        var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
        var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

        var guildId = guildConfiguration.Gw2GuildMemberRoleId;
        var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds?.Split(',').ToList() ?? [];

        if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
        {
            logger.LogDebug("Skipping role sync for guild {GuildName} ({GuildId}): one or more role IDs are not configured (primary={PrimaryRoleId}, secondary={SecondaryRoleId}, verified={VerifiedRoleId})",
                guild.Name, guild.Id, primaryRoleId, secondaryRoleId, verifiedRoleId);
            return;
        }

        logger.LogInformation("Starting role sync for guild {GuildName} ({GuildId}): {UserCount} members, primary GW2 guild={Gw2GuildId}, secondary count={SecondaryCount}",
            guild.Name, guild.Id, guild.Users.Count, guildId ?? "none", secondaryGuildIds.Count);

        foreach (var user in guild.Users)
        {
            try
            {
                var playerAccount = accounts.FirstOrDefault(f => f.DiscordId == (long)user.Id);
                var playerGwAccounts = gwAccounts.Where(s => s.DiscordId == (long)user.Id).ToList();

                if (playerAccount == null)
                {
                    logger.LogDebug("Skipping {UserDisplayName} ({UserId}): no DonBot account record", user.DisplayName, user.Id);
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value, "no DonBot account");
                    continue;
                }

                if (!playerGwAccounts.Any() || playerGwAccounts.All(s => string.IsNullOrEmpty(s.GuildWarsApiKey)))
                {
                    logger.LogDebug("Skipping {UserDisplayName} ({UserId}): no linked GW2 accounts or all API keys are empty", user.DisplayName, user.Id);
                    await RemoveAllRolesFromUser(user, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value, "no linked GW2 API key");
                    continue;
                }

                if (!guildWars2Data.TryGetValue(playerAccount.DiscordId, out var accountData))
                {
                    logger.LogDebug("Skipping role changes for {UserDisplayName} ({UserId}): no GW2 data fetched this cycle (transient API failure), keeping existing roles",
                        user.DisplayName, user.Id);
                    continue;
                }

                await HandleUserRoles(user, accountData, primaryRoleId.Value, secondaryRoleId.Value, verifiedRoleId.Value, guildId, secondaryGuildIds);
            }
            catch (HttpException ex) when (IsUnknownMember(ex))
            {
                logger.LogInformation("Skipping role updates for {UserDisplayName} ({UserId}): Discord no longer recognizes this guild member",
                    user.DisplayName, user.Id);
            }
            catch (HttpException ex)
            {
                logger.LogWarning(ex, "Failed to update roles for {UserDisplayName} ({UserId}); continuing role sync",
                    user.DisplayName, user.Id);
            }
        }

    }

    private async Task RemoveAllRolesFromUser(SocketGuildUser user, long primaryRoleId, long secondaryRoleId, long verifiedRoleId, string reason)
    {
        var roles = user.Roles.Select(s => s.Id).ToList();
        var removedRoles = new List<string>();

        if (roles.Contains((ulong)primaryRoleId))
        {
            await user.RemoveRoleAsync((ulong)primaryRoleId);
            removedRoles.Add("primary");
        }

        if (roles.Contains((ulong)secondaryRoleId))
        {
            await user.RemoveRoleAsync((ulong)secondaryRoleId);
            removedRoles.Add("secondary");
        }

        if (roles.Contains((ulong)verifiedRoleId))
        {
            await user.RemoveRoleAsync((ulong)verifiedRoleId);
            removedRoles.Add("verified");
        }

        if (removedRoles.Count > 0)
        {
            logger.LogWarning("Removed [{RemovedRoles}] from {UserDisplayName} ({UserId}): {Reason}",
                string.Join(", ", removedRoles), user.DisplayName, user.Id, reason);
        }
    }

    private async Task HandleUserRoles(SocketGuildUser user, List<GuildWars2AccountDataModel> accountData, long primaryRoleId, long secondaryRoleId, long verifiedRoleId, string? guildId, List<string> secondaryGuildIds)
    {
        var userGuilds = accountData.SelectMany(s => s.Guilds).ToList();
        var inPrimary = guildId != null && userGuilds.Contains(guildId);
        var inSecondary = secondaryGuildIds.Any(userGuilds.Contains);

        var roles = user.Roles.Select(s => s.Id).ToList();

        logger.LogDebug("Evaluating roles for {UserDisplayName} ({UserId}): inPrimary={InPrimary}, inSecondary={InSecondary}, gw2Accounts={AccountCount}",
            user.DisplayName, user.Id, inPrimary, inSecondary, accountData.Count);

        await HandlePrimaryGuildRole(user, inPrimary, primaryRoleId, roles);
        await HandleSecondaryGuildRoles(user, inSecondary, secondaryRoleId, roles);
        await HandleVerifiedRole(user, verifiedRoleId, roles);
    }

    private async Task HandlePrimaryGuildRole(SocketGuildUser user, bool inPrimaryGuild, long primaryRoleId, List<ulong> roles)
    {
        if (roles.Contains((ulong)primaryRoleId) && !inPrimaryGuild)
        {
            await user.RemoveRoleAsync((ulong)primaryRoleId);
            logger.LogInformation("Removed primary role from {UserDisplayName} ({UserId}): no longer in primary GW2 guild", user.DisplayName, user.Id);
        }
        else if (!roles.Contains((ulong)primaryRoleId) && inPrimaryGuild)
        {
            await user.AddRoleAsync((ulong)primaryRoleId);
            logger.LogInformation("Added primary role to {UserDisplayName} ({UserId}): confirmed member of primary GW2 guild", user.DisplayName, user.Id);
        }
    }

    private async Task HandleSecondaryGuildRoles(SocketGuildUser user, bool inSecondaryGuild, long secondaryRoleId, List<ulong> roles)
    {
        if (roles.Contains((ulong)secondaryRoleId) && !inSecondaryGuild)
        {
            await user.RemoveRoleAsync((ulong)secondaryRoleId);
            logger.LogInformation("Removed secondary role from {UserDisplayName} ({UserId}): no longer in any secondary GW2 guild", user.DisplayName, user.Id);
        }
        else if (!roles.Contains((ulong)secondaryRoleId) && inSecondaryGuild)
        {
            await user.AddRoleAsync((ulong)secondaryRoleId);
            logger.LogInformation("Added secondary role to {UserDisplayName} ({UserId}): confirmed member of a secondary GW2 guild", user.DisplayName, user.Id);
        }
    }

    private async Task HandleVerifiedRole(SocketGuildUser user, long verifiedRoleId, List<ulong> roles)
    {
        if (!roles.Contains((ulong)verifiedRoleId))
        {
            await user.AddRoleAsync((ulong)verifiedRoleId);
            logger.LogInformation("Added verified role to {UserDisplayName} ({UserId}): GW2 account successfully verified this cycle", user.DisplayName, user.Id);
        }
    }

    private static bool IsUnknownMember(HttpException ex) =>
        ex.DiscordCode.HasValue && (int)ex.DiscordCode.Value == UnknownMemberDiscordCode;
}
