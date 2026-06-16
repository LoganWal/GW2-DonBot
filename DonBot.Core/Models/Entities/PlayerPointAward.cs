using System.ComponentModel.DataAnnotations;

namespace DonBot.Core.Models.Entities;

public class PlayerPointAward
{
    [Key]
    public long PlayerPointAwardId { get; init; }

    public long FightLogId { get; init; }

    public long PlayerFightLogId { get; init; }

    public long DiscordId { get; init; }

    [MaxLength(1000)]
    public string GuildWarsAccountName { get; init; } = string.Empty;

    public short FightType { get; init; }

    [MaxLength(100)]
    public string Metric { get; init; } = string.Empty;

    [MaxLength(100)]
    public string MetricLabel { get; init; } = string.Empty;

    public decimal MetricValue { get; init; }

    public decimal PercentileValue { get; init; }

    public decimal BasePoints { get; init; }

    public decimal Multiplier { get; init; }

    public decimal Points { get; init; }

    [MaxLength(200)]
    public string Reason { get; init; } = string.Empty;

    public DateTime AwardedAt { get; init; }
}
