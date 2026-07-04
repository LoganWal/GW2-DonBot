using System.Data;
using DonBot.Core.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Core.Services.Raffles;

public enum RaffleOperationStatus
{
    Success,
    InvalidRaffleType,
    InvalidPoints,
    DescriptionRequired,
    ActiveRaffleExists,
    RaffleNotFound,
    PreviousRaffleNotFound,
    AccountNotFound,
    GuildWarsAccountRequired,
    InsufficientPoints,
    NoEntries,
    CreatorMismatch
}

public sealed record RaffleEnterRequest(
    long GuildId,
    int RaffleId,
    long DiscordId,
    decimal Points);

public sealed record RaffleEnterActiveRequest(
    long GuildId,
    int RaffleType,
    long DiscordId,
    decimal Points);

public sealed record RaffleCreateRequest(
    long GuildId,
    int RaffleType,
    string? Description,
    long CreatorDiscordId);

public sealed record RaffleCompleteRequest(
    long GuildId,
    int RaffleType,
    int? WinnersCount);

public sealed record RaffleReopenRequest(
    long GuildId,
    int RaffleType,
    long CreatorDiscordId);

public sealed record RaffleUpdateRequest(
    long GuildId,
    int RaffleId,
    long EditorDiscordId,
    string? Description);

public sealed record RaffleMessageReference(
    int RaffleId,
    long MessageChannelId,
    long MessageId);

public sealed record RaffleEnterResult(
    RaffleOperationStatus Status,
    Raffle? Raffle = null,
    PlayerRaffleBid? Bid = null,
    decimal AvailablePoints = 0);

public sealed record RaffleCreateResult(
    RaffleOperationStatus Status,
    Raffle? Raffle = null);

public sealed record RaffleCompleteResult(
    RaffleOperationStatus Status,
    Raffle? Raffle = null,
    IReadOnlyList<PlayerRaffleBid>? Bids = null,
    IReadOnlyList<PlayerRaffleBid>? Winners = null)
{
    public IReadOnlyList<PlayerRaffleBid> Bids { get; } = Bids ?? [];

    public IReadOnlyList<PlayerRaffleBid> Winners { get; } = Winners ?? [];
}

public sealed record RaffleReopenResult(
    RaffleOperationStatus Status,
    Raffle? Raffle = null);

public sealed record RaffleUpdateResult(
    RaffleOperationStatus Status,
    Raffle? Raffle = null);

public sealed record RaffleRandomEntryContextResult(
    RaffleOperationStatus Status,
    decimal AvailablePoints = 0);

public sealed class RaffleService(
    IDbContextFactory<DatabaseContext> dbContextFactory,
    RaffleWinnerSelector winnerSelector)
{
    public async Task<RaffleEnterResult> EnterAsync(
        RaffleEnterRequest request,
        CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var raffle = await ctx.Raffle
            .AsTracking()
            .FirstOrDefaultAsync(r =>
                r.Id == request.RaffleId &&
                r.GuildId == request.GuildId &&
                r.IsActive, ct);

        var result = await EnterAsync(ctx, raffle, request.DiscordId, request.Points, ct);
        if (result.Status != RaffleOperationStatus.Success)
        {
            return result;
        }

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<int?> GetActiveRaffleTypeAsync(
        long guildId,
        int raffleId,
        CancellationToken ct = default)
    {
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        return await ctx.Raffle
            .Where(r => r.Id == raffleId && r.GuildId == guildId && r.IsActive)
            .Select(r => (int?)r.RaffleType)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RaffleEnterResult> EnterActiveAsync(
        RaffleEnterActiveRequest request,
        CancellationToken ct = default)
    {
        if (!RaffleRules.IsValidRaffleType(request.RaffleType))
        {
            return new RaffleEnterResult(RaffleOperationStatus.InvalidRaffleType);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var raffle = await ctx.Raffle
            .AsTracking()
            .FirstOrDefaultAsync(r =>
                r.GuildId == request.GuildId &&
                r.RaffleType == request.RaffleType &&
                r.IsActive, ct);

        var result = await EnterAsync(ctx, raffle, request.DiscordId, request.Points, ct);
        if (result.Status != RaffleOperationStatus.Success)
        {
            return result;
        }

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<RaffleCreateResult> CreateAsync(
        RaffleCreateRequest request,
        CancellationToken ct = default)
    {
        return await CreateCoreAsync(request, createMessageReference: null, ct);
    }

    public async Task<RaffleCreateResult> CreateWithMessageReferenceAsync(
        RaffleCreateRequest request,
        Func<Raffle, CancellationToken, Task<RaffleMessageReference>> createMessageReference,
        CancellationToken ct = default)
    {
        return await CreateCoreAsync(request, createMessageReference, ct);
    }

    private async Task<RaffleCreateResult> CreateCoreAsync(
        RaffleCreateRequest request,
        Func<Raffle, CancellationToken, Task<RaffleMessageReference>>? createMessageReference,
        CancellationToken ct)
    {
        if (!RaffleRules.IsValidRaffleType(request.RaffleType))
        {
            return new RaffleCreateResult(RaffleOperationStatus.InvalidRaffleType);
        }

        var description = RaffleRules.NormalizeDescription(request.Description);
        if (description is null)
        {
            return new RaffleCreateResult(RaffleOperationStatus.DescriptionRequired);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var existingActive = await ctx.Raffle.AnyAsync(r =>
            r.GuildId == request.GuildId &&
            r.RaffleType == request.RaffleType &&
            r.IsActive, ct);
        if (existingActive)
        {
            return new RaffleCreateResult(RaffleOperationStatus.ActiveRaffleExists);
        }

        var raffle = new Raffle
        {
            Description = description,
            GuildId = request.GuildId,
            IsActive = true,
            RaffleType = request.RaffleType,
            CreatorDiscordId = request.CreatorDiscordId
        };

        ctx.Raffle.Add(raffle);
        await ctx.SaveChangesAsync(ct);

        if (createMessageReference is not null)
        {
            var messageReference = await createMessageReference(raffle, ct);
            raffle.MessageChannelId = messageReference.MessageChannelId;
            raffle.MessageId = messageReference.MessageId;
            await ctx.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new RaffleCreateResult(RaffleOperationStatus.Success, raffle);
    }

    public async Task<RaffleCompleteResult> CompleteAsync(
        RaffleCompleteRequest request,
        CancellationToken ct = default)
    {
        return await CompleteCoreAsync(request, announceCompletion: null, ct);
    }

    public async Task<RaffleCompleteResult> CompleteWithAnnouncementAsync(
        RaffleCompleteRequest request,
        Func<Raffle, IReadOnlyList<PlayerRaffleBid>, IReadOnlyList<PlayerRaffleBid>, CancellationToken, Task> announceCompletion,
        CancellationToken ct = default)
    {
        return await CompleteCoreAsync(request, announceCompletion, ct);
    }

    private async Task<RaffleCompleteResult> CompleteCoreAsync(
        RaffleCompleteRequest request,
        Func<Raffle, IReadOnlyList<PlayerRaffleBid>, IReadOnlyList<PlayerRaffleBid>, CancellationToken, Task>? announceCompletion,
        CancellationToken ct)
    {
        if (!RaffleRules.IsValidRaffleType(request.RaffleType))
        {
            return new RaffleCompleteResult(RaffleOperationStatus.InvalidRaffleType);
        }

        var winnersCount = RaffleRules.ResolveWinnersCount(request.RaffleType, request.WinnersCount);
        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var raffle = await ctx.Raffle
            .AsTracking()
            .FirstOrDefaultAsync(r =>
                r.GuildId == request.GuildId &&
                r.RaffleType == request.RaffleType &&
                r.IsActive, ct);
        if (raffle is null)
        {
            return new RaffleCompleteResult(RaffleOperationStatus.RaffleNotFound);
        }

        var bids = await ctx.PlayerRaffleBid
            .AsTracking()
            .Where(b => b.RaffleId == raffle.Id && b.PointsSpent > 0)
            .OrderByDescending(b => b.PointsSpent)
            .ToListAsync(ct);
        if (bids.Count == 0)
        {
            return new RaffleCompleteResult(RaffleOperationStatus.NoEntries, raffle);
        }

        var winners = winnerSelector.PickWinners(bids, winnersCount);
        var winnerDiscordIds = winners.Select(w => w.DiscordId).ToHashSet();
        foreach (var bid in bids)
        {
            bid.IsWinner = winnerDiscordIds.Contains(bid.DiscordId);
        }

        raffle.IsActive = false;

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (announceCompletion is not null)
        {
            await announceCompletion(raffle, bids, winners, ct);
        }

        return new RaffleCompleteResult(
            RaffleOperationStatus.Success,
            raffle,
            bids,
            winners);
    }

    public async Task<RaffleReopenResult> ReopenAsync(
        RaffleReopenRequest request,
        CancellationToken ct = default)
    {
        return await ReopenCoreAsync(request, createMessageReference: null, ct);
    }

    public async Task<RaffleReopenResult> ReopenWithMessageReferenceAsync(
        RaffleReopenRequest request,
        Func<Raffle, CancellationToken, Task<RaffleMessageReference>> createMessageReference,
        CancellationToken ct = default)
    {
        return await ReopenCoreAsync(request, createMessageReference, ct);
    }

    private async Task<RaffleReopenResult> ReopenCoreAsync(
        RaffleReopenRequest request,
        Func<Raffle, CancellationToken, Task<RaffleMessageReference>>? createMessageReference,
        CancellationToken ct)
    {
        if (!RaffleRules.IsValidRaffleType(request.RaffleType))
        {
            return new RaffleReopenResult(RaffleOperationStatus.InvalidRaffleType);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var existingActive = await ctx.Raffle.AnyAsync(r =>
            r.GuildId == request.GuildId &&
            r.RaffleType == request.RaffleType &&
            r.IsActive, ct);
        if (existingActive)
        {
            return new RaffleReopenResult(RaffleOperationStatus.ActiveRaffleExists);
        }

        var raffle = await ctx.Raffle.AsTracking()
            .Where(r =>
                r.GuildId == request.GuildId &&
                r.RaffleType == request.RaffleType)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (raffle is null)
        {
            return new RaffleReopenResult(RaffleOperationStatus.PreviousRaffleNotFound);
        }

        raffle.IsActive = true;
        raffle.CreatorDiscordId ??= request.CreatorDiscordId;
        await ResetWinnersAsync(ctx, raffle.Id, ct);

        if (createMessageReference is not null)
        {
            var messageReference = await createMessageReference(raffle, ct);
            raffle.MessageChannelId = messageReference.MessageChannelId;
            raffle.MessageId = messageReference.MessageId;
        }

        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new RaffleReopenResult(RaffleOperationStatus.Success, raffle);
    }

    public async Task<RaffleUpdateResult> UpdateAsync(
        RaffleUpdateRequest request,
        CancellationToken ct = default)
    {
        var description = RaffleRules.NormalizeDescription(request.Description);
        if (description is null)
        {
            return new RaffleUpdateResult(RaffleOperationStatus.DescriptionRequired);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var raffle = await ctx.Raffle.AsTracking().FirstOrDefaultAsync(r =>
            r.Id == request.RaffleId &&
            r.GuildId == request.GuildId &&
            r.IsActive, ct);
        if (raffle is null)
        {
            return new RaffleUpdateResult(RaffleOperationStatus.RaffleNotFound);
        }

        if (raffle.CreatorDiscordId != request.EditorDiscordId)
        {
            return new RaffleUpdateResult(RaffleOperationStatus.CreatorMismatch, raffle);
        }

        raffle.Description = description;
        await ctx.SaveChangesAsync(ct);
        return new RaffleUpdateResult(RaffleOperationStatus.Success, raffle);
    }

    public async Task<RaffleRandomEntryContextResult> GetRandomEntryContextAsync(
        long guildId,
        int raffleType,
        long discordId,
        CancellationToken ct = default)
    {
        if (!RaffleRules.IsValidRaffleType(raffleType))
        {
            return new RaffleRandomEntryContextResult(RaffleOperationStatus.InvalidRaffleType);
        }

        await using var ctx = await dbContextFactory.CreateDbContextAsync(ct);
        var hasActiveRaffle = await ctx.Raffle.AnyAsync(r =>
            r.GuildId == guildId &&
            r.RaffleType == raffleType &&
            r.IsActive, ct);
        if (!hasActiveRaffle)
        {
            return new RaffleRandomEntryContextResult(RaffleOperationStatus.RaffleNotFound);
        }

        var account = await ctx.Account.FirstOrDefaultAsync(a => a.DiscordId == discordId, ct);
        if (account is null)
        {
            return new RaffleRandomEntryContextResult(RaffleOperationStatus.AccountNotFound);
        }

        var hasGw2Account = await ctx.GuildWarsAccount.AnyAsync(a => a.DiscordId == discordId, ct);
        if (!hasGw2Account)
        {
            return new RaffleRandomEntryContextResult(
                RaffleOperationStatus.GuildWarsAccountRequired,
                account.AvailablePoints);
        }

        if (account.AvailablePoints <= 1)
        {
            return new RaffleRandomEntryContextResult(
                RaffleOperationStatus.InsufficientPoints,
                account.AvailablePoints);
        }

        return new RaffleRandomEntryContextResult(
            RaffleOperationStatus.Success,
            account.AvailablePoints);
    }

    private static async Task<RaffleEnterResult> EnterAsync(
        DatabaseContext ctx,
        Raffle? raffle,
        long discordId,
        decimal points,
        CancellationToken ct)
    {
        if (points <= 0)
        {
            return new RaffleEnterResult(RaffleOperationStatus.InvalidPoints);
        }

        if (raffle is null)
        {
            return new RaffleEnterResult(RaffleOperationStatus.RaffleNotFound);
        }

        var account = await ctx.Account.AsTracking().FirstOrDefaultAsync(a => a.DiscordId == discordId, ct);
        if (account is null)
        {
            return new RaffleEnterResult(RaffleOperationStatus.AccountNotFound, raffle);
        }

        var hasGw2Account = await ctx.GuildWarsAccount.AnyAsync(a => a.DiscordId == discordId, ct);
        if (!hasGw2Account)
        {
            return new RaffleEnterResult(
                RaffleOperationStatus.GuildWarsAccountRequired,
                raffle,
                AvailablePoints: account.AvailablePoints);
        }

        if (account.AvailablePoints < points)
        {
            return new RaffleEnterResult(
                RaffleOperationStatus.InsufficientPoints,
                raffle,
                AvailablePoints: account.AvailablePoints);
        }

        var bid = await ctx.PlayerRaffleBid.AsTracking()
            .FirstOrDefaultAsync(b => b.RaffleId == raffle.Id && b.DiscordId == discordId, ct);
        if (bid is null)
        {
            bid = new PlayerRaffleBid
            {
                RaffleId = raffle.Id,
                DiscordId = discordId,
                PointsSpent = points
            };
            ctx.PlayerRaffleBid.Add(bid);
        }
        else
        {
            bid.PointsSpent += points;
        }

        account.AvailablePoints -= points;
        return new RaffleEnterResult(
            RaffleOperationStatus.Success,
            raffle,
            bid,
            account.AvailablePoints);
    }

    private static async Task ResetWinnersAsync(
        DatabaseContext ctx,
        int raffleId,
        CancellationToken ct)
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
}
