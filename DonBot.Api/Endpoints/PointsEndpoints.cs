using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.Rest;
using DonBot.Api.Services;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DonBot.Api.Endpoints;

public static class PointsEndpoints
{
    private const int MaxRaffleDescriptionLength = 4000;
    private static readonly TimeSpan GuildListCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GuildListErrorTtl = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapPointsEndpoints(this WebApplication app)
    {
        var pointsGroup = app.MapGroup("/api/points").RequireAuthorization();
        pointsGroup.MapGet("/me", GetMyPoints);

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

    public record GuildSummaryDto(string GuildId, string GuildName);

    public record RaffleStateDto(
        string GuildId,
        string GuildName,
        AccountDto? Account,
        IReadOnlyList<RaffleDto> Raffles,
        IReadOnlyList<RaffleHistoryDto> LastRaffles,
        RafflePermissionsDto Permissions,
        RaffleAvailabilityDto Availability);

    public record AccountDto(decimal Points, decimal AvailablePoints);

    public record RaffleDto(
        int Id,
        int RaffleType,
        string Type,
        string Description,
        bool IsActive,
        bool CanEdit,
        decimal UserBid,
        decimal TotalPoints,
        IReadOnlyList<RaffleBidDto> TopBidders);

    public record RaffleBidDto(string DiscordId, string DisplayName, decimal PointsSpent);

    public record RaffleHistoryDto(
        int Id,
        int RaffleType,
        string Type,
        string Description,
        decimal TotalPoints,
        IReadOnlyList<RaffleHistoryBidDto> Winners,
        IReadOnlyList<RaffleHistoryBidDto> Bids);

    public record RaffleHistoryBidDto(string DiscordId, string DisplayName, decimal PointsSpent, bool IsWinner);

    public record RafflePermissionsDto(
        bool CanEnterRaffle,
        bool CanEnterEventRaffle,
        bool CanCreateRaffle,
        bool CanCreateEventRaffle,
        bool CanCompleteRaffle,
        bool CanCompleteEventRaffle,
        bool CanReopenRaffle,
        bool CanReopenEventRaffle);

    public record RaffleAvailabilityDto(
        bool HasPreviousRaffle,
        bool HasPreviousEventRaffle);

    public record EnterRaffleDto(int RaffleId, int Points);

    public record RaffleWriteDto(int RaffleType, string? Description);

    public record CompleteRaffleDto(int RaffleType, int? WinnersCount);

    public record RaffleWinnerDto(string DiscordId, string DisplayName, decimal PointsSpent);

    public record RaffleCompletedDto(
        int RaffleId,
        int RaffleType,
        string Type,
        string Description,
        DateTimeOffset DrawAtUtc,
        IReadOnlyList<RaffleWinnerDto> Winners);

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
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] IMemoryCache cache)
    {
        var discordId = user.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return Results.Unauthorized();
        }

        var cacheKey = $"raffle:guilds:{discordId}";
        var result = await cache.GetOrCoalesceAsync(cacheKey, GuildListCacheTtl, GuildListErrorTtl, async () =>
        {
            var userGuildList = await userGuilds.GetForPrincipalAsync(user);
            if (userGuildList is null)
            {
                return new List<GuildSummaryDto>();
            }

            var userGuildIds = userGuildList.Select(g => (long)g.Id).ToHashSet();

            await using var ctx = await dbContextFactory.CreateDbContextAsync();
            return await ctx.Guild
                .Where(g => userGuildIds.Contains(g.GuildId))
                .OrderBy(g => g.GuildName)
                .Select(g => new GuildSummaryDto(g.GuildId.ToString(), g.GuildName ?? g.GuildId.ToString()))
                .ToListAsync();
        });

        return Results.Ok(result);
    }

    private static async Task<IResult> GetRaffleState(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        CancellationToken ct)
    {
        if (await EnsureMemberAsync(user, guildId, userGuilds, ct) is { } denied)
        {
            return denied;
        }

        return Results.Ok(await BuildRaffleStateAsync(guildId, user, commandAccess, dbContextFactory, ct));
    }

    private static async Task StreamRaffleUpdates(
        long guildId,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] IRaffleEventHub eventHub,
        [FromServices] IHostApplicationLifetime lifetime,
        HttpContext httpContext,
        CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.ApplicationStopping);
        ct = linkedCts.Token;

        if (!await userGuilds.IsMemberAsync(user, (ulong)guildId, ct))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

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
                    await WriteEvent(httpContext, "state", state, ct);
                }

                while (subscription.Reader.TryRead(out var message))
                {
                    await WriteEvent(httpContext, message.Name, message.Payload, ct);
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
                    await httpContext.Response.WriteAsync(": heartbeat\n\n", ct);
                    await httpContext.Response.Body.FlushAsync(ct);
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
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        CancellationToken ct)
    {
        if (await EnsureMemberAsync(user, guildId, userGuilds, ct) is { } denied)
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

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var raffle = await ctx.Raffle
            .Select(r => new { r.Id, r.GuildId, r.IsActive, r.RaffleType })
            .FirstOrDefaultAsync(r => r.Id == body.RaffleId && r.GuildId == guildId && r.IsActive, ct);
        if (raffle is null)
        {
            return Results.NotFound(new { error = "That raffle is no longer active." });
        }

        var commandName = raffle.RaffleType == (int)RaffleTypeEnum.Event ? "enter_event_raffle" : "enter_raffle";
        if (!await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        var account = await ctx.Account.AsTracking().FirstOrDefaultAsync(a => a.DiscordId == discordId, ct);
        if (account is null)
        {
            return Results.BadRequest(new { error = "Verify a GW2 account before entering raffles." });
        }

        var hasGw2Account = await ctx.GuildWarsAccount.AnyAsync(a => a.DiscordId == discordId, ct);
        if (!hasGw2Account)
        {
            return Results.BadRequest(new { error = "Verify a GW2 account before entering raffles." });
        }

        if (account.AvailablePoints < body.Points)
        {
            return Results.BadRequest(new { error = $"You only have {Math.Floor(account.AvailablePoints)} points available." });
        }

        var bid = await ctx.PlayerRaffleBid.AsTracking()
            .FirstOrDefaultAsync(b => b.RaffleId == raffle.Id && b.DiscordId == discordId, ct);
        if (bid is null)
        {
            bid = new PlayerRaffleBid
            {
                RaffleId = raffle.Id,
                DiscordId = discordId,
                PointsSpent = body.Points
            };
            ctx.PlayerRaffleBid.Add(bid);
        }
        else
        {
            bid.PointsSpent += body.Points;
        }

        account.AvailablePoints -= body.Points;
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Ok(new { bid.PointsSpent, account.AvailablePoints });
    }

    private static async Task<IResult> CreateRaffle(
        long guildId,
        RaffleWriteDto body,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (await EnsureMemberAsync(user, guildId, userGuilds, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }
        if (!IsValidRaffleType(body.RaffleType))
        {
            return Results.BadRequest(new { error = "Invalid raffle type." });
        }

        var commandName = body.RaffleType == (int)RaffleTypeEnum.Event ? "create_event_raffle" : "create_raffle";
        if (!await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        var description = NormalizeDescription(body.Description);
        if (description is null)
        {
            return Results.BadRequest(new { error = "Raffle message is required." });
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var existingActive = await ctx.Raffle.AnyAsync(r =>
            r.GuildId == guildId && r.RaffleType == body.RaffleType && r.IsActive, ct);
        if (existingActive)
        {
            return Results.Conflict(new { error = "There is already an active raffle of this type." });
        }

        var guild = await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        var channel = await ResolveAnnouncementChannelAsync(guild, clientProvider);
        if (guild is null || channel is null)
        {
            return Results.BadRequest(new { error = "This server needs an announcement channel configured." });
        }

        var raffle = new Raffle
        {
            Description = description,
            GuildId = guildId,
            IsActive = true,
            RaffleType = body.RaffleType,
            CreatorDiscordId = discordId
        };

        var sent = await channel.SendMessageAsync(
            text: BuildRaffleMention(guild),
            embeds: [await BuildRaffleEmbedAsync(raffle, guild.GuildId, footerService)],
            components: BuildRaffleComponents(raffle.RaffleType, guildId, configuration, httpContext));

        raffle.MessageChannelId = (long)channel.Id;
        raffle.MessageId = (long)sent.Id;
        ctx.Raffle.Add(raffle);
        await ctx.SaveChangesAsync(ct);

        return Results.Ok(new { raffle.Id });
    }

    private static async Task<IResult> CompleteRaffle(
        long guildId,
        CompleteRaffleDto body,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IRaffleEventHub eventHub,
        CancellationToken ct)
    {
        if (await EnsureMemberAsync(user, guildId, userGuilds, ct) is { } denied)
        {
            return denied;
        }
        if (!IsValidRaffleType(body.RaffleType))
        {
            return Results.BadRequest(new { error = "Invalid raffle type." });
        }

        var isEvent = body.RaffleType == (int)RaffleTypeEnum.Event;
        var commandName = isEvent ? "complete_event_raffle" : "complete_raffle";
        if (!await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        var winnersCount = isEvent ? Math.Max(1, body.WinnersCount ?? 1) : 1;

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        var raffle = await ctx.Raffle.AsTracking().FirstOrDefaultAsync(r =>
            r.GuildId == guildId && r.RaffleType == body.RaffleType && r.IsActive, ct);
        if (raffle is null)
        {
            return Results.NotFound(new { error = "There is no active raffle of this type." });
        }

        var bids = await ctx.PlayerRaffleBid
            .AsTracking()
            .Where(b => b.RaffleId == raffle.Id && b.PointsSpent > 0)
            .OrderByDescending(b => b.PointsSpent)
            .ToListAsync(ct);
        if (bids.Count == 0)
        {
            return Results.BadRequest(new { error = "No one has entered this raffle yet." });
        }

        var winners = PickWinners(bids, winnersCount);
        var winnerDiscordIds = winners.Select(w => w.DiscordId).ToHashSet();
        foreach (var bid in bids)
        {
            bid.IsWinner = winnerDiscordIds.Contains(bid.DiscordId);
        }

        raffle.IsActive = false;
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var guild = await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        var channel = await ResolveAnnouncementChannelAsync(guild, clientProvider);
        var winnerDtos = await BuildWinnerDtosAsync(ctx, winners, ct);
        if (guild != null && channel != null)
        {
            await channel.SendMessageAsync(
                text: BuildRaffleMention(guild),
                embeds: [await BuildRaffleResultEmbedAsync(raffle, guildId, winnerDtos, bids, footerService, ctx, ct)]);
        }

        var payload = new RaffleCompletedDto(
            raffle.Id,
            raffle.RaffleType,
            RaffleTypeName(raffle.RaffleType),
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
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDiscordCommandAccessService commandAccess,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (await EnsureMemberAsync(user, guildId, userGuilds, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }
        if (!IsValidRaffleType(body.RaffleType))
        {
            return Results.BadRequest(new { error = "Invalid raffle type." });
        }

        var commandName = body.RaffleType == (int)RaffleTypeEnum.Event ? "reopen_event_raffle" : "reopen_raffle";
        if (!await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, commandName, ct))
        {
            return CommandForbidden(commandName);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var existingActive = await ctx.Raffle.AnyAsync(r =>
            r.GuildId == guildId && r.RaffleType == body.RaffleType && r.IsActive, ct);
        if (existingActive)
        {
            return Results.Conflict(new { error = "There is already an active raffle of this type." });
        }

        var raffle = await ctx.Raffle.AsTracking()
            .Where(r => r.GuildId == guildId && r.RaffleType == body.RaffleType)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (raffle is null)
        {
            return Results.NotFound(new { error = "There is no previous raffle of this type." });
        }

        var guild = await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        var channel = await ResolveAnnouncementChannelAsync(guild, clientProvider);
        if (guild is null || channel is null)
        {
            return Results.BadRequest(new { error = "This server needs an announcement channel configured." });
        }

        raffle.IsActive = true;
        raffle.CreatorDiscordId ??= discordId;
        await ResetRaffleWinnersAsync(ctx, raffle.Id, ct);

        var sent = await channel.SendMessageAsync(
            text: BuildRaffleMention(guild),
            embeds: [await BuildRaffleEmbedAsync(raffle, guildId, footerService, reopened: true)],
            components: BuildRaffleComponents(raffle.RaffleType, guildId, configuration, httpContext));

        raffle.MessageChannelId = (long)channel.Id;
        raffle.MessageId = (long)sent.Id;
        await ctx.SaveChangesAsync(ct);

        return Results.Ok(new { raffle.Id });
    }

    private static async Task<IResult> UpdateRaffle(
        long guildId,
        int raffleId,
        RaffleWriteDto body,
        ClaimsPrincipal user,
        [FromServices] IUserGuildsService userGuilds,
        [FromServices] IDbContextFactory<DatabaseContext> dbContextFactory,
        [FromServices] DiscordRestClientProvider clientProvider,
        [FromServices] IFooterService footerService,
        [FromServices] IConfiguration configuration,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (await EnsureMemberAsync(user, guildId, userGuilds, ct) is { } denied)
        {
            return denied;
        }
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        var description = NormalizeDescription(body.Description);
        if (description is null)
        {
            return Results.BadRequest(new { error = "Raffle message is required." });
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var raffle = await ctx.Raffle.AsTracking().FirstOrDefaultAsync(r =>
            r.Id == raffleId && r.GuildId == guildId && r.IsActive, ct);
        if (raffle is null)
        {
            return Results.NotFound(new { error = "That raffle is no longer active." });
        }
        if (raffle.CreatorDiscordId != discordId)
        {
            return Results.Json(
                new { error = "Only the user who created this raffle can edit its Discord message." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        raffle.Description = description;
        await ctx.SaveChangesAsync(ct);

        var guild = await ctx.Guild.FirstOrDefaultAsync(g => g.GuildId == guildId, ct);
        await TryUpdateDiscordRaffleMessageAsync(raffle, guildId, clientProvider, footerService, configuration, httpContext);

        return Results.Ok(await ToRaffleDtoAsync(ctx, raffle, discordId, guild?.GuildName, ct));
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
            raffleDtos.Add(await ToRaffleDtoAsync(ctx, raffle, discordId, guild?.GuildName, ct));
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
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "enter_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "enter_event_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "create_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "create_event_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "complete_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "complete_event_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "reopen_raffle", ct),
                await commandAccess.HasCommandAccessAsync(user, (ulong)guildId, "reopen_event_raffle", ct)),
            new RaffleAvailabilityDto(
                previousTypes.Contains((int)RaffleTypeEnum.Normal),
                previousTypes.Contains((int)RaffleTypeEnum.Event)));
    }

    private static async Task<RaffleDto> ToRaffleDtoAsync(
        DatabaseContext ctx,
        Raffle raffle,
        long discordId,
        string? guildName,
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
            RaffleTypeName(raffle.RaffleType),
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
            RaffleTypeName(raffle.RaffleType),
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
                DisplayName(b.DiscordId, namesByDiscordId),
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
                DisplayName(b.DiscordId, namesByDiscordId),
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
                DisplayName(w.DiscordId, namesByDiscordId),
                w.PointsSpent))
            .ToList();
    }

    private static async Task ResetRaffleWinnersAsync(DatabaseContext ctx, int raffleId, CancellationToken ct)
    {
        var winnerBids = await ctx.PlayerRaffleBid
            .AsTracking()
            .Where(b => b.RaffleId == raffleId && b.IsWinner)
            .ToListAsync(ct);
        foreach (var bid in winnerBids)
        {
            bid.IsWinner = false;
        }
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

    private static string DisplayName(long discordId, IReadOnlyDictionary<long, string> gw2Names) =>
        gw2Names.TryGetValue(discordId, out var names) && !string.IsNullOrWhiteSpace(names)
            ? names
            : $"Discord {discordId}";

    private static IReadOnlyList<PlayerRaffleBid> PickWinners(IReadOnlyList<PlayerRaffleBid> bids, int requestedCount)
    {
        var remaining = bids.Select(b => new PlayerRaffleBid
        {
            RaffleId = b.RaffleId,
            DiscordId = b.DiscordId,
            PointsSpent = b.PointsSpent
        }).ToList();
        var winners = new List<PlayerRaffleBid>();

        while (remaining.Count > 0 && winners.Count < requestedCount)
        {
            var total = remaining.Sum(b => b.PointsSpent);
            if (total <= 0)
            {
                break;
            }

            var picked = (decimal)Random.Shared.NextDouble() * total;
            var rolling = 0m;
            for (var i = 0; i < remaining.Count; i++)
            {
                rolling += remaining[i].PointsSpent;
                if (picked <= rolling)
                {
                    winners.Add(remaining[i]);
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }

        return winners;
    }

    private static async Task<Embed> BuildRaffleEmbedAsync(
        Raffle raffle,
        long guildId,
        IFooterService footerService,
        bool reopened = false)
    {
        var isEvent = raffle.RaffleType == (int)RaffleTypeEnum.Event;
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
        if (raffle.RaffleType == (int)RaffleTypeEnum.Event)
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
            Title = raffle.RaffleType == (int)RaffleTypeEnum.Event ? "Event Raffle Results!" : "Raffle!",
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
        var isEvent = raffleType == (int)RaffleTypeEnum.Event;
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
        HttpContext? httpContext)
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
        catch
        {
        }
    }

    private static async Task<RestTextChannel?> ResolveAnnouncementChannelAsync(Guild? guild, DiscordRestClientProvider clientProvider)
    {
        if (guild?.AnnouncementChannelId == null)
        {
            return null;
        }

        var client = await clientProvider.GetClientAsync();
        return await client.GetChannelAsync((ulong)guild.AnnouncementChannelId.Value) as RestTextChannel;
    }

    private static EmbedAuthorBuilder DonBotAuthor() => new()
    {
        Name = "GW2-DonBot",
        Url = "https://github.com/LoganWal/GW2-DonBot",
        IconUrl = "https://i.imgur.com/tQ4LD6H.png"
    };

    private static string BuildRaffleMention(Guild guild) =>
        guild.DiscordVerifiedRoleId.HasValue ? $"<@&{guild.DiscordVerifiedRoleId.Value}>" : "";

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length <= MaxRaffleDescriptionLength
            ? trimmed
            : trimmed[..MaxRaffleDescriptionLength];
    }

    private static bool IsValidRaffleType(int raffleType) =>
        raffleType is (int)RaffleTypeEnum.Normal or (int)RaffleTypeEnum.Event;

    private static string RaffleTypeName(int raffleType) =>
        raffleType == (int)RaffleTypeEnum.Event ? "Event" : "Normal";

    private static bool TryGetDiscordId(ClaimsPrincipal user, out long discordId)
    {
        var discordIdStr = user.FindFirst("discord_id")?.Value;
        return long.TryParse(discordIdStr, out discordId);
    }

    private static async Task<IResult?> EnsureMemberAsync(
        ClaimsPrincipal user,
        long guildId,
        IUserGuildsService userGuilds,
        CancellationToken ct)
    {
        if (await userGuilds.IsMemberAsync(user, (ulong)guildId, ct))
        {
            return null;
        }

        return Results.Json(
            new { error = "You do not have access to this server." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    private static IResult CommandForbidden(string commandName) =>
        Results.Json(
            new { error = $"Discord does not allow your account to use /{commandName} in this server." },
            statusCode: StatusCodes.Status403Forbidden);

    private static async Task WriteEvent(HttpContext httpContext, string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJsonOptions);
        await httpContext.Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await httpContext.Response.Body.FlushAsync(ct);
    }

    private static async Task<IResult> GetDashboard(
        ClaimsPrincipal user,
        IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        if (!TryGetDiscordId(user, out var discordId))
        {
            return Results.Unauthorized();
        }

        await using var context = await dbContextFactory.CreateDbContextAsync();

        var account = await context.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId);

        var gw2Accounts = await context.GuildWarsAccount
            .Where(a => a.DiscordId == discordId)
            .ToListAsync();

        var gw2Names = gw2Accounts
            .Select(a => a.GuildWarsAccountName)
            .Where(n => n != null)
            .ToList();

        if (gw2Names.Count == 0)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate = (DateTime?)null, fights = (object?)null });
        }

        var joined = from pfl in context.PlayerFightLog
                     join fl in context.FightLog on pfl.FightLogId equals fl.FightLogId
                     where gw2Names.Contains(pfl.GuildWarsAccountName)
                     select new { Pfl = pfl, fl.FightType, fl.FightStart };

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
                LastFightDate = (DateTime?)g.Max(x => x.FightStart)
            })
            .FirstOrDefaultAsync();

        if (totals is null)
        {
            return Results.Ok(new { account, gw2Accounts, lastFightDate = (DateTime?)null, fights = (object?)null });
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

        var characterCount = await joined
            .Where(x => x.Pfl.CharacterName != null && x.Pfl.CharacterName != "")
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
            bestDamageFight,
            bestKillsFight
        };

        return Results.Ok(new { account, gw2Accounts, lastFightDate = totals.LastFightDate, fights, characterCount });
    }
}
