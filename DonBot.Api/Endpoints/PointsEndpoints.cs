using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.Rest;
using DonBot.Api.Services;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Services;
using DonBot.Core.Services.Raffles;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Endpoints;

public static class PointsEndpoints
{
    public static void MapPointsEndpoints(this WebApplication app)
    {
        var pointsGroup = app.MapGroup("/api/points").RequireAuthorization();
        pointsGroup.MapGet("/me", GetMyPoints);
        pointsGroup.MapGet("/history", GetPointHistory);

        var rafflesGroup = app.MapGroup("/api/raffles").RequireAuthorization();
        rafflesGroup.MapGet("/", GetRaffles);
        rafflesGroup.MapGet("/guilds", ListRaffleGuilds);
        rafflesGroup.MapGet("/{guildId:long}", GetRaffleState);
        rafflesGroup.MapGet("/{guildId:long}/stream", StreamRaffleUpdates);
        rafflesGroup.MapPost("/{guildId:long}/enter", EnterRaffle);
        rafflesGroup.MapPost("/{guildId:long}/create", CreateRaffle);
        rafflesGroup.MapPost("/{guildId:long}/complete", CompleteRaffle);
        rafflesGroup.MapPost("/{guildId:long}/reopen", ReopenRaffle);
        rafflesGroup.MapPut("/{guildId:long}/{raffleId:int}", UpdateRaffle);

        var dashboardGroup = app.MapGroup("/api/dashboard").RequireAuthorization();
        dashboardGroup.MapGet("/", GetDashboard);
    }

    private static readonly TimeSpan GuildListCacheTtl = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan GuildListErrorTtl = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    // ASP.NET Core model binding and System.Text.Json use these DTO members implicitly.
    // ReSharper disable ClassNeverInstantiated.Local
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    // ReSharper disable UnusedMember.Local
    private sealed class GuildSummaryDto(string guildId, string guildName)
    {
        public string GuildId { get; } = guildId;
        public string GuildName { get; } = guildName;
    }

    private sealed class RaffleStateDto(
        string guildId,
        string guildName,
        AccountDto? account,
        IReadOnlyList<RaffleDto> raffles,
        IReadOnlyList<RaffleHistoryDto> lastRaffles,
        RafflePermissionsDto permissions,
        RaffleAvailabilityDto availability)
    {
        public string GuildId { get; } = guildId;
        public string GuildName { get; } = guildName;
        public AccountDto? Account { get; } = account;
        public IReadOnlyList<RaffleDto> Raffles { get; } = raffles;
        public IReadOnlyList<RaffleHistoryDto> LastRaffles { get; } = lastRaffles;
        public RafflePermissionsDto Permissions { get; } = permissions;
        public RaffleAvailabilityDto Availability { get; } = availability;
    }

    private sealed class AccountDto(decimal points, decimal availablePoints)
    {
        public decimal Points { get; } = points;
        public decimal AvailablePoints { get; } = availablePoints;
    }

    private sealed class RaffleDto(
        int id,
        int raffleType,
        string type,
        string description,
        bool isActive,
        bool canEdit,
        decimal userBid,
        decimal totalPoints,
        IReadOnlyList<RaffleBidDto> topBidders)
    {
        public int Id { get; } = id;
        public int RaffleType { get; } = raffleType;
        public string Type { get; } = type;
        public string Description { get; } = description;
        public bool IsActive { get; } = isActive;
        public bool CanEdit { get; } = canEdit;
        public decimal UserBid { get; } = userBid;
        public decimal TotalPoints { get; } = totalPoints;
        public IReadOnlyList<RaffleBidDto> TopBidders { get; } = topBidders;
    }

    private sealed class RaffleBidDto(string discordId, string displayName, decimal pointsSpent)
    {
        public string DiscordId { get; } = discordId;
        public string DisplayName { get; } = displayName;
        public decimal PointsSpent { get; } = pointsSpent;
    }

    private sealed class RaffleHistoryDto(
        int id,
        int raffleType,
        string type,
        string description,
        decimal totalPoints,
        IReadOnlyList<RaffleHistoryBidDto> winners,
        IReadOnlyList<RaffleHistoryBidDto> bids)
    {
        public int Id { get; } = id;
        public int RaffleType { get; } = raffleType;
        public string Type { get; } = type;
        public string Description { get; } = description;
        public decimal TotalPoints { get; } = totalPoints;
        public IReadOnlyList<RaffleHistoryBidDto> Winners { get; } = winners;
        public IReadOnlyList<RaffleHistoryBidDto> Bids { get; } = bids;
    }

    private sealed class RaffleHistoryBidDto(string discordId, string displayName, decimal pointsSpent, bool isWinner)
    {
        public string DiscordId { get; } = discordId;
        public string DisplayName { get; } = displayName;
        public decimal PointsSpent { get; } = pointsSpent;
        public bool IsWinner { get; } = isWinner;
    }

    private sealed class RafflePermissionsDto(
        bool canEnterRaffle,
        bool canEnterEventRaffle,
        bool canCreateRaffle,
        bool canCreateEventRaffle,
        bool canCompleteRaffle,
        bool canCompleteEventRaffle,
        bool canReopenRaffle,
        bool canReopenEventRaffle)
    {
        public bool CanEnterRaffle { get; } = canEnterRaffle;
        public bool CanEnterEventRaffle { get; } = canEnterEventRaffle;
        public bool CanCreateRaffle { get; } = canCreateRaffle;
        public bool CanCreateEventRaffle { get; } = canCreateEventRaffle;
        public bool CanCompleteRaffle { get; } = canCompleteRaffle;
        public bool CanCompleteEventRaffle { get; } = canCompleteEventRaffle;
        public bool CanReopenRaffle { get; } = canReopenRaffle;
        public bool CanReopenEventRaffle { get; } = canReopenEventRaffle;
    }

    private sealed class RaffleAvailabilityDto(bool hasPreviousRaffle, bool hasPreviousEventRaffle)
    {
        public bool HasPreviousRaffle { get; } = hasPreviousRaffle;
        public bool HasPreviousEventRaffle { get; } = hasPreviousEventRaffle;
    }

    private sealed class EnterRaffleDto
    {
        public int RaffleId { get; set; }
        public int Points { get; set; }
    }

    private sealed class RaffleWriteDto
    {
        public int RaffleType { get; set; }
        public string? Description { get; set; }
    }

    private sealed class CompleteRaffleDto
    {
        public int RaffleType { get; set; }
        public int? WinnersCount { get; set; }
    }

    private sealed class RaffleWinnerDto(string discordId, string displayName, decimal pointsSpent)
    {
        public string DiscordId { get; } = discordId;
        public string DisplayName { get; } = displayName;
        public decimal PointsSpent { get; } = pointsSpent;
    }

    private sealed class RaffleCompletedDto(
        int raffleId,
        int raffleType,
        string type,
        string description,
        DateTimeOffset drawAtUtc,
        IReadOnlyList<RaffleWinnerDto> winners)
    {
        public int RaffleId { get; } = raffleId;
        public int RaffleType { get; } = raffleType;
        public string Type { get; } = type;
        public string Description { get; } = description;
        public DateTimeOffset DrawAtUtc { get; } = drawAtUtc;
        public IReadOnlyList<RaffleWinnerDto> Winners { get; } = winners;
    }

    private sealed class RaffleAnnouncementException(string message) : Exception(message);
    // ReSharper restore UnusedMember.Local
    // ReSharper restore UnusedAutoPropertyAccessor.Local
    // ReSharper restore ClassNeverInstantiated.Local

    private static async Task<IResult> GetMyPoints(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();
        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId);
        if (account is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(account);
    }

    private static async Task<IResult> GetPointHistory(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int take = 100,
        CancellationToken ct = default)
    {
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        take = Math.Clamp(take, 1, 250);

        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId, ct);

        var rows = await (
            from award in context.PlayerPointAward
            join fight in context.FightLog on award.FightLogId equals fight.FightLogId
            where award.DiscordId == discordId
            select new { Award = award, Fight = fight })
            .ToListAsync(ct);

        var recent = rows
            .GroupBy(r => new
            {
                r.Award.FightLogId,
                r.Award.PlayerFightLogId,
                r.Award.GuildWarsAccountName,
                r.Fight.FightType,
                r.Fight.FightStart,
                r.Fight.Url
            })
            .Select(g => new
            {
                fightLogId = g.Key.FightLogId,
                playerFightLogId = g.Key.PlayerFightLogId,
                accountName = g.Key.GuildWarsAccountName,
                fightType = g.Key.FightType,
                fightStart = g.Key.FightStart,
                url = g.Key.Url,
                awardedAt = g.Max(r => r.Award.AwardedAt),
                awardId = g.Max(r => r.Award.PlayerPointAwardId),
                totalPoints = Math.Round(g.Sum(r => r.Award.Points), 3),
                components = g
                    .OrderByDescending(r => r.Award.Points)
                    .Select(r => new
                    {
                        r.Award.Metric,
                        r.Award.MetricLabel,
                        r.Award.MetricValue,
                        r.Award.PercentileValue,
                        r.Award.BasePoints,
                        r.Award.Multiplier,
                        r.Award.Points,
                        r.Award.Reason,
                    })
                    .ToList()
            })
            .OrderByDescending(r => r.awardedAt)
            .ThenByDescending(r => r.awardId)
            .Take(take)
            .Select(r => new
            {
                r.fightLogId,
                r.playerFightLogId,
                r.accountName,
                r.fightType,
                r.fightStart,
                r.url,
                r.totalPoints,
                r.components
            })
            .ToList();

        var last30Start = DateTime.UtcNow.AddDays(-30);
        var summary = new
        {
            totalEarned = account?.Points ?? rows.Sum(r => r.Award.Points),
            availablePoints = account?.AvailablePoints ?? 0m,
            spentPoints = account is null ? 0m : Math.Max(account.Points - account.AvailablePoints, 0m),
            earnedLast30Days = Math.Round(rows.Where(r => r.Award.AwardedAt >= last30Start).Sum(r => r.Award.Points), 3),
            awardedLogs = rows.Select(r => r.Award.FightLogId).Distinct().Count(),
            lastAwardAt = rows.Count > 0 ? rows.Max(r => r.Award.AwardedAt) : (DateTime?)null,
        };

        var byComponent = rows
            .GroupBy(r => new { r.Award.Metric, r.Award.MetricLabel })
            .Select(g => new
            {
                metric = g.Key.Metric,
                metricLabel = g.Key.MetricLabel,
                points = Math.Round(g.Sum(r => r.Award.Points), 3),
                count = g.Count(),
            })
            .OrderByDescending(g => g.points)
            .ThenBy(g => g.metricLabel)
            .ToList();

        var byFightType = rows
            .GroupBy(r => r.Fight.FightType)
            .Select(g => new
            {
                fightType = g.Key,
                points = Math.Round(g.Sum(r => r.Award.Points), 3),
                count = g.Select(r => r.Award.FightLogId).Distinct().Count(),
            })
            .OrderByDescending(g => g.points)
            .ThenBy(g => g.fightType)
            .ToList();

        return Results.Ok(new { account, summary, byComponent, byFightType, recent });
    }

    private static async Task<IResult> GetRaffles(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var activeRaffles = await context.Raffle
            .Where(r => r.IsActive)
            .ToListAsync();

        var raffleIds = activeRaffles.Select(r => r.Id).ToList();

        var userBids = await context.PlayerRaffleBid
            .Where(b => b.DiscordId == discordId && raffleIds.Contains(b.RaffleId))
            .ToListAsync();

        return Results.Ok(new { raffles = activeRaffles, userBids });
    }

    private static async Task<IResult> ListRaffleGuilds(
        ClaimsPrincipal user,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] AccessibleGuildsCache accessibleGuildsCache,
        CancellationToken ct)
    {
        var result = await accessibleGuildsCache.GetAsync<GuildSummaryDto>(
            user,
            "raffle:guilds",
            GuildListCacheTtl,
            GuildListErrorTtl,
            async (userGuildList, cancellationToken) =>
        {
            var userGuildIds = userGuildList
                .Where(g => g.Id <= long.MaxValue)
                .Select(g => (long)g.Id)
                .ToHashSet();

            await using var ctx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await ctx.Guild
                .Where(g => userGuildIds.Contains(g.GuildId))
                .OrderBy(g => g.GuildName)
                .Select(g => new GuildSummaryDto(g.GuildId.ToString(), g.GuildName ?? g.GuildId.ToString()))
                .ToListAsync(cancellationToken);
        },
            ct);

        if (result.IsUnauthorized)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(result.Guilds);
    }

    private static async Task<IResult> GetRaffleState(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        CancellationToken ct)
    {
        if (await RequireRaffleMemberAsync(user, guildId, accessGuard, ct) is { } denied)
        {
            return denied;
        }

        return Results.Ok(await BuildRaffleStateAsync(guildId, user, commandAccess, dbContextFactory, ct));
    }

    private static async Task StreamRaffleUpdates(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] IRaffleEventHub eventHub,
        [FromServices] IHostApplicationLifetime lifetime,
        HttpContext httpContext,
        CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.ApplicationStopping);
        ct = linkedCts.Token;

        if (!GuildRouteParser.TryNormalize(guildId, out _))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (await accessGuard.RequireMemberAsync(user, guildId, ct) is not null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        SseWriter.Prepare(httpContext.Response);

        using var subscription = eventHub.Subscribe(guildId);
        var pollInterval = TimeSpan.FromSeconds(2);
        var heartbeatInterval = TimeSpan.FromSeconds(20);
        var lastHeartbeat = DateTime.UtcNow;
        string? lastStateJson = null;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var state = await BuildRaffleStateAsync(guildId, user, commandAccess, dbContextFactory, ct);
                var stateJson = JsonSerializer.Serialize(state, SseJsonOptions);
                if (stateJson != lastStateJson)
                {
                    lastStateJson = stateJson;
                    await WriteEvent(httpContext.Response, "state", state, ct);
                }

                while (subscription.Reader.TryRead(out var message))
                {
                    await WriteEvent(httpContext.Response, message.Name, message.Payload, ct);
                }

                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var waitTask = subscription.Reader.WaitToReadAsync(waitCts.Token).AsTask();
                var delayTask = Task.Delay(pollInterval, ct);
                var completed = await Task.WhenAny(waitTask, delayTask);
                if (completed == waitTask)
                {
                    if (await waitTask)
                    {
                        continue;
                    }

                    break;
                }

                await waitCts.CancelAsync();
                try
                {
                    await waitTask;
                }
                catch (OperationCanceledException)
                {
                }

                if (DateTime.UtcNow - lastHeartbeat >= heartbeatInterval)
                {
                    await SseWriter.WriteCommentAsync(httpContext.Response, "heartbeat", ct);
                    lastHeartbeat = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static async Task<IResult> EnterRaffle(
        long guildId,
        EnterRaffleDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] RaffleService raffleService,
        CancellationToken ct)
    {
        if (await RequireRaffleMemberAsync(user, guildId, accessGuard, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }
        if (body.Points <= 0)
        {
            return Results.BadRequest(new { error = "Spend at least 1 point." });
        }

        var raffleType = await raffleService.GetActiveRaffleTypeAsync(guildId, body.RaffleId, ct);
        if (raffleType is null)
        {
            return Results.NotFound(new { error = "That raffle is no longer active." });
        }

        var commandName = RaffleRules.IsEventRaffle(raffleType.Value) ? "enter_event_raffle" : "enter_raffle";
        if (!await HasCommandAccessAsync(commandAccess, user, guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        var result = await raffleService.EnterAsync(new RaffleEnterRequest(guildId, body.RaffleId, discordId, body.Points), ct);
        return result.Status switch
        {
            RaffleOperationStatus.Success => Results.Ok(new { result.Bid!.PointsSpent, result.AvailablePoints }),
            RaffleOperationStatus.InvalidPoints => Results.BadRequest(new { error = "Spend at least 1 point." }),
            RaffleOperationStatus.RaffleNotFound => Results.NotFound(new { error = "That raffle is no longer active." }),
            RaffleOperationStatus.AccountNotFound or RaffleOperationStatus.GuildWarsAccountRequired =>
                Results.BadRequest(new { error = "Verify a GW2 account before entering raffles." }),
            RaffleOperationStatus.InsufficientPoints =>
                Results.BadRequest(new { error = $"You only have {Math.Floor(result.AvailablePoints)} points available." }),
            _ => Results.BadRequest(new { error = "Unable to enter raffle." })
        };
    }

    private static async Task<IResult> CreateRaffle(
        long guildId,
        RaffleWriteDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] RaffleService raffleService,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (await RequireRaffleMemberAsync(user, guildId, accessGuard, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }
        if (!RaffleRules.IsValidRaffleType(body.RaffleType))
        {
            return Results.BadRequest(new { error = "Invalid raffle type." });
        }

        var commandName = RaffleRules.IsEventRaffle(body.RaffleType) ? "create_event_raffle" : "create_raffle";
        if (!await HasCommandAccessAsync(commandAccess, user, guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        RaffleCreateResult result;
        try
        {
            result = await raffleService.CreateWithMessageReferenceAsync(
                new RaffleCreateRequest(
                    guildId,
                    body.RaffleType,
                    body.Description,
                    discordId),
                async (raffle, cancellationToken) =>
                {
                    await using var callbackCtx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var guild = await callbackCtx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, cancellationToken);
                    var channel = await ResolveAnnouncementChannelAsync(guild, clientProvider);
                    if (guild is null || channel is null)
                    {
                        throw new RaffleAnnouncementException("This server needs an announcement channel configured.");
                    }

                    var sent = await channel.SendMessageAsync(
                        text: BuildRaffleMention(guild),
                        embeds: [await BuildRaffleEmbedAsync(raffle, guild.GuildId, footerService)],
                        components: BuildRaffleComponents(raffle.RaffleType, guildId, configuration, httpContext));
                    return new RaffleMessageReference(
                        raffle.Id,
                        (long)channel.Id,
                        (long)sent.Id);
                },
                ct);
        }
        catch (RaffleAnnouncementException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        if (result.Status != RaffleOperationStatus.Success)
        {
            return result.Status switch
            {
                RaffleOperationStatus.DescriptionRequired => Results.BadRequest(new { error = "Raffle message is required." }),
                RaffleOperationStatus.ActiveRaffleExists => Results.Conflict(new { error = "There is already an active raffle of this type." }),
                RaffleOperationStatus.InvalidRaffleType => Results.BadRequest(new { error = "Invalid raffle type." }),
                _ => Results.BadRequest(new { error = "Unable to create raffle." })
            };
        }

        var raffle = result.Raffle!;

        return Results.Ok(new { raffle.Id });
    }

    private static async Task<IResult> CompleteRaffle(
        long guildId,
        CompleteRaffleDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] RaffleService raffleService,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IRaffleEventHub eventHub,
        CancellationToken ct)
    {
        if (await RequireRaffleMemberAsync(user, guildId, accessGuard, ct) is { } denied)
        {
            return denied;
        }
        if (!RaffleRules.IsValidRaffleType(body.RaffleType))
        {
            return Results.BadRequest(new { error = "Invalid raffle type." });
        }

        var isEvent = RaffleRules.IsEventRaffle(body.RaffleType);
        var commandName = isEvent ? "complete_event_raffle" : "complete_raffle";
        if (!await HasCommandAccessAsync(commandAccess, user, guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        IReadOnlyList<RaffleWinnerDto> winnerDtos = [];
        RaffleCompleteResult result;
        try
        {
            result = await raffleService.CompleteWithAnnouncementAsync(
                new RaffleCompleteRequest(
                    guildId,
                    body.RaffleType,
                    body.WinnersCount),
                async (raffle, bids, winners, cancellationToken) =>
                {
                    await using var callbackCtx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var guild = await callbackCtx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, cancellationToken);
                    winnerDtos = await BuildWinnerDtosAsync(callbackCtx, winners, cancellationToken);
                    if (guild is null)
                    {
                        return;
                    }

                    var channel = await ResolveAnnouncementChannelAsync(guild, clientProvider);
                    if (channel is null)
                    {
                        return;
                    }

                    await channel.SendMessageAsync(
                        text: BuildRaffleMention(guild),
                        embeds: [await BuildRaffleResultEmbedAsync(raffle, guildId, winnerDtos, bids, footerService, callbackCtx, cancellationToken)]);
                },
                ct);
        }
        catch (RaffleAnnouncementException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        if (result.Status != RaffleOperationStatus.Success)
        {
            return result.Status switch
            {
                RaffleOperationStatus.RaffleNotFound => Results.NotFound(new { error = "There is no active raffle of this type." }),
                RaffleOperationStatus.NoEntries => Results.BadRequest(new { error = "No one has entered this raffle yet." }),
                RaffleOperationStatus.InvalidRaffleType => Results.BadRequest(new { error = "Invalid raffle type." }),
                _ => Results.BadRequest(new { error = "Unable to complete raffle." })
            };
        }

        var raffle = result.Raffle!;

        var payload = new RaffleCompletedDto(
            raffle.Id,
            raffle.RaffleType,
            RaffleRules.TypeName(raffle.RaffleType),
            raffle.Description ?? "",
            DateTimeOffset.UtcNow.AddSeconds(5),
            winnerDtos);
        eventHub.Publish(guildId, "completed", payload);

        return Results.Ok(payload);
    }

    private static async Task<IResult> ReopenRaffle(
        long guildId,
        RaffleWriteDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] RaffleService raffleService,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (await RequireRaffleMemberAsync(user, guildId, accessGuard, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }
        if (!RaffleRules.IsValidRaffleType(body.RaffleType))
        {
            return Results.BadRequest(new { error = "Invalid raffle type." });
        }

        var commandName = RaffleRules.IsEventRaffle(body.RaffleType) ? "reopen_event_raffle" : "reopen_raffle";
        if (!await HasCommandAccessAsync(commandAccess, user, guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        RaffleReopenResult result;
        try
        {
            result = await raffleService.ReopenWithMessageReferenceAsync(
                new RaffleReopenRequest(
                    guildId,
                    body.RaffleType,
                    discordId),
                async (raffle, cancellationToken) =>
                {
                    await using var callbackCtx = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                    var guild = await callbackCtx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, cancellationToken);
                    var channel = await ResolveAnnouncementChannelAsync(guild, clientProvider);
                    if (guild is null || channel is null)
                    {
                        throw new RaffleAnnouncementException("This server needs an announcement channel configured.");
                    }

                    var sent = await channel.SendMessageAsync(
                        text: BuildRaffleMention(guild),
                        embeds: [await BuildRaffleEmbedAsync(raffle, guildId, footerService, reopened: true)],
                        components: BuildRaffleComponents(raffle.RaffleType, guildId, configuration, httpContext));
                    return new RaffleMessageReference(
                        raffle.Id,
                        (long)channel.Id,
                        (long)sent.Id);
                },
                ct);
        }
        catch (RaffleAnnouncementException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        if (result.Status != RaffleOperationStatus.Success)
        {
            return result.Status switch
            {
                RaffleOperationStatus.ActiveRaffleExists => Results.Conflict(new { error = "There is already an active raffle of this type." }),
                RaffleOperationStatus.PreviousRaffleNotFound => Results.NotFound(new { error = "There is no previous raffle of this type." }),
                RaffleOperationStatus.InvalidRaffleType => Results.BadRequest(new { error = "Invalid raffle type." }),
                _ => Results.BadRequest(new { error = "Unable to reopen raffle." })
            };
        }

        var raffle = result.Raffle!;

        return Results.Ok(new { raffle.Id });
    }

    private static async Task<IResult> UpdateRaffle(
        long guildId,
        int raffleId,
        RaffleWriteDto body,
        ClaimsPrincipal user,
        [FromServices] GuildAccessGuard accessGuard,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] RaffleService raffleService,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IConfiguration configuration,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (await RequireRaffleMemberAsync(user, guildId, accessGuard, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        var updateResult = await raffleService.UpdateAsync(new RaffleUpdateRequest(
            guildId,
            raffleId,
            discordId,
            body.Description), ct);
        if (updateResult.Status == RaffleOperationStatus.DescriptionRequired)
        {
            return Results.BadRequest(new { error = "Raffle message is required." });
        }
        if (updateResult.Status == RaffleOperationStatus.RaffleNotFound)
        {
            return Results.NotFound(new { error = "That raffle is no longer active." });
        }
        if (updateResult.Status == RaffleOperationStatus.CreatorMismatch)
        {
            return Results.Json(
                new { error = "Only the user who created this raffle can edit its Discord message." },
                statusCode: StatusCodes.Status403Forbidden);
        }
        if (updateResult.Status != RaffleOperationStatus.Success)
        {
            return Results.BadRequest(new { error = "Unable to update raffle." });
        }

        var raffle = updateResult.Raffle!;
        await TryUpdateDiscordRaffleMessageAsync(
            raffle,
            guildId,
            clientProvider,
            footerService,
            configuration,
            httpContext,
            loggerFactory.CreateLogger("DonBot.Api.Endpoints.PointsEndpoints"));

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        return Results.Ok(await ToRaffleDtoAsync(ctx, raffle, discordId, ct));
    }

    private static async Task<RaffleStateDto> BuildRaffleStateAsync(
        long guildId,
        ClaimsPrincipal user,
        IDiscordCommandAccessService commandAccess,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        CancellationToken ct)
    {
        TryGetDiscordId(user, out var discordId);
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);

        var guild = await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        var account = await ctx.Account
            .Where(a => a.DiscordId == discordId)
            .Select(a => new AccountDto(a.Points, a.AvailablePoints))
            .FirstOrDefaultAsync(ct);

        var activeRaffles = await ctx.Raffle
            .Where(r => r.GuildId == guildId && r.IsActive)
            .OrderBy(r => r.RaffleType)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);
        var previousTypes = await ctx.Raffle
            .Where(r => r.GuildId == guildId && !r.IsActive)
            .Select(r => r.RaffleType)
            .Distinct()
            .ToListAsync(ct);

        var raffleDtos = new List<RaffleDto>();
        foreach (var raffle in activeRaffles)
        {
            raffleDtos.Add(await ToRaffleDtoAsync(ctx, raffle, discordId, ct));
        }

        var lastRaffles = new List<RaffleHistoryDto>();
        foreach (var raffleType in new[] { (int)RaffleTypeEnum.Normal, (int)RaffleTypeEnum.Event })
        {
            var lastRaffle = await ctx.Raffle
                .Where(r => r.GuildId == guildId && r.RaffleType == raffleType && !r.IsActive)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(ct);
            if (lastRaffle is not null)
            {
                lastRaffles.Add(await ToRaffleHistoryDtoAsync(ctx, lastRaffle, ct));
            }
        }

        return new RaffleStateDto(
            guildId.ToString(),
            guild?.GuildName ?? guildId.ToString(),
            account,
            raffleDtos,
            lastRaffles,
            new RafflePermissionsDto(
                await HasCommandAccessAsync(commandAccess, user, guildId, "enter_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "enter_event_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "create_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "create_event_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "complete_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "complete_event_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "reopen_raffle", ct),
                await HasCommandAccessAsync(commandAccess, user, guildId, "reopen_event_raffle", ct)),
            new RaffleAvailabilityDto(
                previousTypes.Contains((int)RaffleTypeEnum.Normal),
                previousTypes.Contains((int)RaffleTypeEnum.Event)));
    }

    private static async Task<RaffleDto> ToRaffleDtoAsync(
        DatabaseContext ctx,
        Raffle raffle,
        long discordId,
        CancellationToken ct)
    {
        var bids = await ctx.PlayerRaffleBid
            .Where(b => b.RaffleId == raffle.Id)
            .OrderByDescending(b => b.PointsSpent)
            .ToListAsync(ct);
        var userBid = bids.FirstOrDefault(b => b.DiscordId == discordId)?.PointsSpent ?? 0;
        var top = await BuildTopBiddersAsync(ctx, bids, 5, ct);

        return new RaffleDto(
            raffle.Id,
            raffle.RaffleType,
            RaffleRules.TypeName(raffle.RaffleType),
            raffle.Description ?? "",
            raffle.IsActive,
            raffle.CreatorDiscordId == discordId,
            userBid,
            bids.Sum(b => b.PointsSpent),
            top);
    }

    private static async Task<RaffleHistoryDto> ToRaffleHistoryDtoAsync(
        DatabaseContext ctx,
        Raffle raffle,
        CancellationToken ct)
    {
        var bids = await ctx.PlayerRaffleBid
            .Where(b => b.RaffleId == raffle.Id)
            .OrderByDescending(b => b.PointsSpent)
            .ToListAsync(ct);
        var bidDtos = await BuildHistoryBidsAsync(ctx, bids, ct);

        return new RaffleHistoryDto(
            raffle.Id,
            raffle.RaffleType,
            RaffleRules.TypeName(raffle.RaffleType),
            raffle.Description ?? "",
            bids.Sum(b => b.PointsSpent),
            bidDtos.Where(b => b.IsWinner).ToList(),
            bidDtos);
    }

    private static async Task<IReadOnlyList<RaffleBidDto>> BuildTopBiddersAsync(
        DatabaseContext ctx,
        IReadOnlyList<PlayerRaffleBid> bids,
        int count,
        CancellationToken ct)
    {
        var selected = bids.OrderByDescending(b => b.PointsSpent).Take(count).ToList();
        var ids = selected.Select(b => b.DiscordId).ToList();
        var namesByDiscordId = await GetGw2NamesByDiscordIdAsync(ctx, ids, ct);
        return selected
            .Select(b => new RaffleBidDto(
                b.DiscordId.ToString(),
                BuildDisplayName(b.DiscordId, namesByDiscordId),
                b.PointsSpent))
            .ToList();
    }

    private static async Task<IReadOnlyList<RaffleHistoryBidDto>> BuildHistoryBidsAsync(
        DatabaseContext ctx,
        IReadOnlyList<PlayerRaffleBid> bids,
        CancellationToken ct)
    {
        var ids = bids.Select(b => b.DiscordId).Distinct().ToList();
        var namesByDiscordId = await GetGw2NamesByDiscordIdAsync(ctx, ids, ct);
        return bids
            .Select(b => new RaffleHistoryBidDto(
                b.DiscordId.ToString(),
                BuildDisplayName(b.DiscordId, namesByDiscordId),
                b.PointsSpent,
                b.IsWinner))
            .ToList();
    }

    private static async Task<IReadOnlyList<RaffleWinnerDto>> BuildWinnerDtosAsync(
        DatabaseContext ctx,
        IReadOnlyList<PlayerRaffleBid> winners,
        CancellationToken ct)
    {
        var ids = winners.Select(w => w.DiscordId).ToList();
        var namesByDiscordId = await GetGw2NamesByDiscordIdAsync(ctx, ids, ct);
        return winners
            .Select(w => new RaffleWinnerDto(
                w.DiscordId.ToString(),
                BuildDisplayName(w.DiscordId, namesByDiscordId),
                w.PointsSpent))
            .ToList();
    }

    private static async Task<Dictionary<long, string>> GetGw2NamesByDiscordIdAsync(DatabaseContext ctx, IReadOnlyList<long> discordIds, CancellationToken ct)
    {
        var accounts = await ctx.GuildWarsAccount
            .Where(a => discordIds.Contains(a.DiscordId) && a.GuildWarsAccountName != null && a.GuildWarsAccountName != "")
            .Select(a => new { a.DiscordId, a.GuildWarsAccountName })
            .ToListAsync(ct);

        return accounts
            .GroupBy(a => a.DiscordId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(a => a.GuildWarsAccountName).Take(2)));
    }

    private static string BuildDisplayName(long discordId, IReadOnlyDictionary<long, string> gw2Names) =>
        gw2Names.TryGetValue(discordId, out var names) && !string.IsNullOrWhiteSpace(names)
            ? names
            : $"Discord {discordId}";

    private static async Task<Embed> BuildRaffleEmbedAsync(
        Raffle raffle,
        long guildId,
        IFooterService footerService,
        bool reopened = false)
    {
        var isEvent = RaffleRules.IsEventRaffle(raffle.RaffleType);
        var title = isEvent ? "Event Raffle!" : "Raffle!";
        var enterCommand = isEvent ? "/enter_event_raffle" : "/enter_raffle";
        var prefix = reopened ? "Reopened raffle. " : "";

        return new EmbedBuilder
        {
            Title = title,
            Description = $"{prefix}{raffle.Description}\nUse /points or the web page to check points.\nUse {enterCommand} <points> or the buttons below to enter.",
            Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
            Author = DonBotAuthor(),
            Footer = new EmbedFooterBuilder
            {
                Text = await footerService.Generate(guildId),
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Timestamp = DateTime.Now
        }.Build();
    }

    private static async Task<Embed> BuildRaffleResultEmbedAsync(
        Raffle raffle,
        long guildId,
        IReadOnlyList<RaffleWinnerDto> winners,
        IReadOnlyList<PlayerRaffleBid> bids,
        IFooterService footerService,
        DatabaseContext ctx,
        CancellationToken ct)
    {
        var top = await BuildTopBiddersAsync(ctx, bids, 5, ct);
        var description = new StringBuilder();
        if (RaffleRules.IsEventRaffle(raffle.RaffleType))
        {
            description.AppendLine("And the winners are:");
            for (var i = 0; i < winners.Count; i++)
            {
                description.AppendLine($"{i + 1}. <@{winners[i].DiscordId}> ({winners[i].DisplayName}) - Bid: {winners[i].PointsSpent} points");
            }
        }
        else
        {
            var winner = winners[0];
            description.AppendLine($"And the winner is! <@{winner.DiscordId}> ({winner.DisplayName}) - Bid: {winner.PointsSpent} points");
        }

        description.AppendLine();
        description.AppendLine("Top 5 Bidders:");
        foreach (var bidder in top)
        {
            description.AppendLine($"<@{bidder.DiscordId}> ({bidder.DisplayName}) - Bid: {bidder.PointsSpent} points");
        }

        return new EmbedBuilder
        {
            Title = RaffleRules.IsEventRaffle(raffle.RaffleType) ? "Event Raffle Results!" : "Raffle!",
            Description = description.ToString(),
            Color = (Color)System.Drawing.Color.FromArgb(230, 231, 232),
            Author = DonBotAuthor(),
            Footer = new EmbedFooterBuilder
            {
                Text = await footerService.Generate(guildId),
                IconUrl = "https://i.imgur.com/tQ4LD6H.png"
            },
            Timestamp = DateTime.Now
        }.Build();
    }

    private static MessageComponent BuildRaffleComponents(
        int raffleType,
        long guildId,
        IConfiguration configuration,
        HttpContext? httpContext = null)
    {
        var isEvent = RaffleRules.IsEventRaffle(raffleType);
        var builder = new ComponentBuilder()
            .WithButton("Points", "Raffle_Points", ButtonStyle.Success)
            .WithButton("1 Point", isEvent ? "Spend_Event_1_Raffle" : "Spend_1_Raffle")
            .WithButton("50 Points", isEvent ? "Spend_Event_50_Raffle" : "Spend_50_Raffle")
            .WithButton("100 Points", isEvent ? "Spend_Event_100_Raffle" : "Spend_100_Raffle")
            .WithButton("1000 Points", isEvent ? "Spend_Event_1000_Raffle" : "Spend_1000_Raffle", ButtonStyle.Danger)
            .WithButton("Random!", isEvent ? "Spend_Event_Random_Raffle" : "Spend_Random_Raffle", ButtonStyle.Success, row: 1);

        builder.WithButton("Open Raffle Page", style: ButtonStyle.Link, url: BuildRafflePageUrl(guildId, configuration, httpContext), row: 1);

        return builder.Build();
    }

    private static string BuildRafflePageUrl(long guildId, IConfiguration configuration, HttpContext? httpContext)
    {
        var webAppBaseUrl = configuration["WebApp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(webAppBaseUrl))
        {
            webAppBaseUrl = httpContext?.Request.Headers.Origin.FirstOrDefault();
        }
        if (string.IsNullOrWhiteSpace(webAppBaseUrl))
        {
            webAppBaseUrl = configuration["Nuxt:BaseUrl"];
        }
        if (string.IsNullOrWhiteSpace(webAppBaseUrl))
        {
            webAppBaseUrl = "http://localhost:3000";
        }

        return $"{webAppBaseUrl.TrimEnd('/')}/points?guild={guildId}";
    }

    private static async Task TryUpdateDiscordRaffleMessageAsync(
        Raffle raffle,
        long guildId,
        DiscordRestClientProvider clientProvider,
        IFooterService footerService,
        IConfiguration configuration,
        HttpContext? httpContext,
        ILogger logger)
    {
        if (!raffle.MessageChannelId.HasValue || !raffle.MessageId.HasValue)
        {
            return;
        }

        try
        {
            var client = await clientProvider.GetClientAsync();
            if (await client.GetChannelAsync((ulong)raffle.MessageChannelId.Value) is not RestTextChannel channel)
            {
                return;
            }
            if (await channel.GetMessageAsync((ulong)raffle.MessageId.Value) is not RestUserMessage message)
            {
                return;
            }

            var embed = await BuildRaffleEmbedAsync(raffle, guildId, footerService);
            await message.ModifyAsync(p =>
            {
                p.Embed = embed;
                p.Components = BuildRaffleComponents(raffle.RaffleType, guildId, configuration, httpContext);
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to update Discord raffle message {MessageId} in channel {ChannelId} for guild {GuildId}.",
                raffle.MessageId,
                raffle.MessageChannelId,
                guildId);
        }
    }

    private static async Task<RestTextChannel?> ResolveAnnouncementChannelAsync(Guild? guild, DiscordRestClientProvider clientProvider)
    {
        if (guild is not { AnnouncementChannelId: { } announcementChannelId })
        {
            return null;
        }

        var client = await clientProvider.GetClientAsync();
        return await client.GetChannelAsync((ulong)announcementChannelId) as RestTextChannel;
    }

    private static EmbedAuthorBuilder DonBotAuthor() => new()
    {
        Name = "GW2-DonBot",
        Url = "https://github.com/LoganWal/GW2-DonBot",
        IconUrl = "https://i.imgur.com/tQ4LD6H.png"
    };

    private static string BuildRaffleMention(Guild guild) =>
        guild.DiscordVerifiedRoleId.HasValue ? $"<@&{guild.DiscordVerifiedRoleId.Value}>" : "";

    private static bool TryGetDiscordId(ClaimsPrincipal user, out long discordId)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        return long.TryParse(discordIdStr, out discordId);
    }

    private static async Task<IResult?> RequireRaffleMemberAsync(
        ClaimsPrincipal user,
        long guildId,
        GuildAccessGuard accessGuard,
        CancellationToken ct)
        => await accessGuard.RequireMemberAsync(
            user,
            guildId,
            ct,
            new { error = "You do not have access to this server." });

    private static IResult CommandForbidden(string commandName) =>
        Results.Json(
            new { error = $"Discord does not allow your account to use /{commandName} in this server." },
            statusCode: StatusCodes.Status403Forbidden);

    private static Task<bool> HasCommandAccessAsync(
        IDiscordCommandAccessService commandAccess,
        ClaimsPrincipal user,
        long guildId,
        string commandName,
        CancellationToken ct)
    {
        return GuildRouteParser.TryNormalize(guildId, out var route)
            ? commandAccess.HasCommandAccessAsync(user, route.UnsignedValue, commandName, ct)
            : Task.FromResult(false);
    }

    private static Task WriteEvent(HttpResponse response, string eventName, object payload, CancellationToken ct) =>
        SseWriter.WriteJsonEventAsync(response, eventName, payload, SseJsonOptions, ct);

    private static async Task<IResult> GetDashboard(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory,
        int? days = null)
    {
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId);
        var pointSummary = await BuildDashboardPointSummaryAsync(context, discordId, days);

        var gw2Accounts = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId)
            .ToListAsync();

        var gw2Names = gw2Accounts
            .Select(a => a.GuildWarsAccountName)
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();

        if (gw2Names.Count == 0)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate = (DateTime?)null, fights = (object?)null, points = pointSummary });
        }

        var joinedBase = from pfl in context.PlayerFightLog
                         join fl in context.FightLog on pfl.FightLogId equals fl.FightLogId
                         where gw2Names.Contains(pfl.GuildWarsAccountName)
                         select new { Pfl = pfl, fl.FightType, fl.FightStart };

        var lastFightDate = await joinedBase
            .GroupBy(_ => 1)
            .Select(g => (DateTime?)g.Max(x => x.FightStart))
            .FirstOrDefaultAsync();

        var joined = joinedBase;
        if (days is > 0)
        {
            var since = DateTime.UtcNow.AddDays(-days.Value);
            joined = joined.Where(x => x.FightStart >= since);
        }

        var totals = await joined
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Wvw = g.Count(x => x.FightType == 0),
                Pve = g.Count(x => x.FightType != 0),
                TotalDamage = g.Sum(x => x.Pfl.Damage),
                TotalKills = g.Sum(x => x.FightType == 0 ? x.Pfl.Kills : 0),
                TotalDeaths = g.Sum(x => x.Pfl.Deaths),
                TotalHealing = g.Sum(x => x.Pfl.Healing),
                TotalCleanses = g.Sum(x => x.Pfl.Cleanses),
                TotalStrips = g.Sum(x => x.Pfl.Strips),
                TotalDownContribution = g.Sum(x => x.FightType == 0 ? x.Pfl.DamageDownContribution : 0L),
                AvgQuickness = g.Average(x => (double)x.Pfl.QuicknessDuration),
                AvgAlac = g.Average(x => (double)x.Pfl.AlacDuration),
                AvgQuicknessGen = g
                    .Where(x => (double)x.Pfl.QuicknessGenGroup > PlayerFightLogRoleClassifier.BoonGenerationThreshold)
                    .Average(x => (double?)x.Pfl.QuicknessGenGroup) ?? 0d,
                AvgAlacGen = g
                    .Where(x => (double)x.Pfl.AlacGenGroup > PlayerFightLogRoleClassifier.BoonGenerationThreshold)
                    .Average(x => (double?)x.Pfl.AlacGenGroup) ?? 0d
            })
            .FirstOrDefaultAsync();

        if (totals is null)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate, fights = (object?)null, points = pointSummary });
        }

        var bestDamageFight = await joined
            .OrderByDescending(x => x.Pfl.Damage)
            .Select(x => new { fightLogId = x.Pfl.FightLogId, fightType = x.FightType, damage = x.Pfl.Damage })
            .FirstOrDefaultAsync();

        var bestKillsFight = await joined
            .Where(x => x.FightType == 0)
            .OrderByDescending(x => x.Pfl.Kills)
            .Select(x => new { fightLogId = x.Pfl.FightLogId, kills = x.Pfl.Kills })
            .FirstOrDefaultAsync();

        var characterCount = await joinedBase
            .Where(x => x.Pfl.CharacterName != "")
            .Select(x => x.Pfl.CharacterName)
            .Distinct()
            .CountAsync();

        var fights = new
        {
            total = totals.Total,
            wvw = totals.Wvw,
            pve = totals.Pve,
            totalDamage = totals.TotalDamage,
            totalKills = totals.TotalKills,
            totalDeaths = totals.TotalDeaths,
            totalHealing = totals.TotalHealing,
            totalCleanses = totals.TotalCleanses,
            totalStrips = totals.TotalStrips,
            totalDownContribution = totals.TotalDownContribution,
            avgQuickness = totals.AvgQuickness,
            avgAlac = totals.AvgAlac,
            avgQuicknessGen = totals.AvgQuicknessGen,
            avgAlacGen = totals.AvgAlacGen,
            bestDamageFight,
            bestKillsFight
        };

        return Results.Ok(new { account, gw2Accounts, lastFightDate, fights, characterCount, points = pointSummary });
    }

    private static async Task<object> BuildDashboardPointSummaryAsync(DatabaseContext context, long discordId, int? days)
    {
        var query = context.PlayerPointAward.Where(a => a.DiscordId == discordId);
        if (days is > 0)
        {
            var since = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(a => a.AwardedAt >= since);
        }

        var rows = await query.ToListAsync();
        if (rows.Count == 0)
        {
            return new
            {
                earned = 0m,
                awardedLogs = 0,
                topComponent = (object?)null,
                lastAwardAt = (DateTime?)null,
            };
        }

        var topComponent = rows
            .GroupBy(a => a.MetricLabel)
            .Select(g => new
            {
                metricLabel = g.Key,
                points = Math.Round(g.Sum(a => a.Points), 3),
                count = g.Count(),
            })
            .OrderByDescending(g => g.points)
            .ThenBy(g => g.metricLabel)
            .FirstOrDefault();

        return new
        {
            earned = Math.Round(rows.Sum(a => a.Points), 3),
            awardedLogs = rows.Select(a => a.FightLogId).Distinct().Count(),
            topComponent,
            lastAwardAt = rows.Max(a => a.AwardedAt),
        };
    }
}
