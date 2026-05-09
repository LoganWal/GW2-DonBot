using Discord.WebSocket;
using DonBot.Models.Apis.GuildWars2Api;
using DonBot.Models.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.DiscordRequestServices;

public sealed class PollingTasksService(
    IEntityService entityService,
    ILogger<PollingTasksService> logger,
    IHttpClientFactory httpClientFactory)
    : IPollingTasksService, IDisposable
{
    // 8 tokens/sec stays safely below GW2's 600/min cap; burst of 8 allows a short initial burst
    private readonly TokenBucketRateLimiter _gw2RateLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 8,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 1000,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokensPerPeriod = 8,
        AutoReplenishment = true
    });

    // Test seam: lets tests skip the retry/rate-limit waits without time-dependent flakes.
    // Production code uses Task.Delay; tests substitute a no-op.
    internal Func<TimeSpan, Task> DelayAsync { get; set; } = Task.Delay;

    public void Dispose() => _gw2RateLimiter.Dispose();

    public async Task PollingRoles(DiscordSocketClient discordClient)
    {
        var accounts = await entityService.Account.GetAllAsync();
        var guilds = await entityService.Guild.GetAllAsync();
        var guildWarsAccounts = await entityService.GuildWarsAccount.GetWhereAsync(s => s.GuildWarsApiKey != null);

        accounts = accounts.Where(s => guildWarsAccounts.Select(gw => gw.DiscordId).Contains(s.DiscordId)).ToList();

        var guildWars2Data = new ConcurrentDictionary<long, List<GuildWars2AccountDataModel>>();

        var tasks = accounts.Select(async account =>
        {
            var guildWars2Accounts = guildWarsAccounts.Where(s => s.DiscordId == account.DiscordId).ToList();
            var accountData = await FetchAccountData(guildWars2Accounts);
            // Only record data when at least one GW2 account was fetched successfully.
            // An empty list means all calls failed this cycle; omitting the entry keeps existing roles intact
            // rather than stripping them on a transient API failure.
            if (accountData.Count > 0)
            {
                guildWars2Data[account.DiscordId] = accountData;
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

    internal async Task<List<GuildWars2AccountDataModel>> FetchAccountData(List<GuildWarsAccount> guildWarsAccounts)
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
                        // Auth failure is permanent - key deleted or missing permissions. Null the key so
                        // HandleGuildUsers strips roles and stops polling this account this cycle.
                        logger.LogWarning("GW2 API key for {GuildWarsAccountName} rejected with {StatusCode}: clearing key",
                            guildWarsAccount.GuildWarsAccountName?.Trim(), response.StatusCode);
                        guildWarsAccount.GuildWarsApiKey = null;
                        break;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var stringData = await response.Content.ReadAsStringAsync();
                        accountData = JsonConvert.DeserializeObject<GuildWars2AccountDataModel>(stringData) ?? new GuildWars2AccountDataModel();

                        guildWarsAccount.World = Convert.ToInt32(accountData.World);
                        guildWarsAccount.GuildWarsGuilds = string.Join(",", accountData.Guilds);

                        success = true;
                        break;
                    }

                    logger.LogWarning("Attempt {Attempt} failed for {GuildWarsAccountName}: {StatusCode}",
                        attempt, guildWarsAccount.GuildWarsAccountName?.Trim(), response.StatusCode);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "FAILED handling {GuildWarsAccountName} on attempt {Attempt}",
                        guildWarsAccount.GuildWarsAccountName?.Trim(), attempt);
                }

                // Exponential backoff with jitter for non-429 failures: ~1s, ~2s, ~4s
                // Jitter avoids synchronized retry waves when many accounts hit the same outage
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

    private async Task HandleGuildUsers(SocketGuild guild, Guild guildConfiguration, ConcurrentDictionary<long, List<GuildWars2AccountDataModel>> guildWars2Data, List<Account> accounts, List<GuildWarsAccount> gwAccounts)
    {
        var primaryRoleId = guildConfiguration.DiscordGuildMemberRoleId;
        var secondaryRoleId = guildConfiguration.DiscordSecondaryMemberRoleId;
        var verifiedRoleId = guildConfiguration.DiscordVerifiedRoleId;

        var guildId = guildConfiguration.Gw2GuildMemberRoleId;
        var secondaryGuildIds = guildConfiguration.Gw2SecondaryMemberRoleIds?.Split(',').ToList() ?? [];

        if (primaryRoleId == null || secondaryRoleId == null || verifiedRoleId == null)
        {
            logger.LogWarning("Skipping role sync for guild {GuildName} ({GuildId}): one or more role IDs are not configured (primary={PrimaryRoleId}, secondary={SecondaryRoleId}, verified={VerifiedRoleId})",
                guild.Name, guild.Id, primaryRoleId, secondaryRoleId, verifiedRoleId);
            return;
        }

        logger.LogInformation("Starting role sync for guild {GuildName} ({GuildId}): {UserCount} members, primary GW2 guild={Gw2GuildId}, secondary count={SecondaryCount}",
            guild.Name, guild.Id, guild.Users.Count, guildId ?? "none", secondaryGuildIds.Count);

        foreach (var user in guild.Users)
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
        var inSecondary = secondaryGuildIds.Any(g => userGuilds.Contains(g));

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
            logger.LogInformation("Removing primary role from {UserDisplayName} ({UserId}): no longer in primary GW2 guild", user.DisplayName, user.Id);
            await user.RemoveRoleAsync((ulong)primaryRoleId);
        }
        else if (!roles.Contains((ulong)primaryRoleId) && inPrimaryGuild)
        {
            logger.LogInformation("Adding primary role to {UserDisplayName} ({UserId}): confirmed member of primary GW2 guild", user.DisplayName, user.Id);
            await user.AddRoleAsync((ulong)primaryRoleId);
        }
    }

    private async Task HandleSecondaryGuildRoles(SocketGuildUser user, bool inSecondaryGuild, long secondaryRoleId, List<ulong> roles)
    {
        if (roles.Contains((ulong)secondaryRoleId) && !inSecondaryGuild)
        {
            logger.LogInformation("Removing secondary role from {UserDisplayName} ({UserId}): no longer in any secondary GW2 guild", user.DisplayName, user.Id);
            await user.RemoveRoleAsync((ulong)secondaryRoleId);
        }
        else if (!roles.Contains((ulong)secondaryRoleId) && inSecondaryGuild)
        {
            logger.LogInformation("Adding secondary role to {UserDisplayName} ({UserId}): confirmed member of a secondary GW2 guild", user.DisplayName, user.Id);
            await user.AddRoleAsync((ulong)secondaryRoleId);
        }
    }

    private async Task HandleVerifiedRole(SocketGuildUser user, long verifiedRoleId, List<ulong> roles)
    {
        if (!roles.Contains((ulong)verifiedRoleId))
        {
            logger.LogInformation("Adding verified role to {UserDisplayName} ({UserId}): GW2 account successfully verified this cycle", user.DisplayName, user.Id);
            await user.AddRoleAsync((ulong)verifiedRoleId);
        }
    }
}
