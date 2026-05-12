using DonBot.Models.Entities;

namespace DonBot.Core.Services.RaidLifecycle;

public enum RaidOpenOutcome
{
    Opened,
    AlreadyOpen,
    GuildNotConfigured
}

public enum RaidCloseOutcome
{
    Closed,
    NoneOpen
}

public sealed record RaidOpenResult(RaidOpenOutcome Outcome, FightsReport? Report);

public sealed record RaidCloseResult(RaidCloseOutcome Outcome, FightsReport? Report);

/// <summary>
/// Shared open/close logic for raids (FightsReport rows). Used by both the Discord
/// slash-command handler and the web API so the two paths stay consistent.
/// </summary>
public interface IRaidLifecycleService
{
    Task<RaidOpenResult> OpenRaidAsync(long guildId, CancellationToken ct = default);

    Task<RaidCloseResult> CloseRaidAsync(long guildId, CancellationToken ct = default);

    Task<FightsReport?> GetLatestRaidAsync(long guildId, CancellationToken ct = default);
}
