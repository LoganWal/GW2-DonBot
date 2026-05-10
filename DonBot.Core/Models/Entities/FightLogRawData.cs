using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class FightLogRawData
{
    [Key]
    public long FightLogId { get; init; }

    // Raw EliteInsights JSON blobs can be several MB - MaxLength set to 100 MB ceiling.
    // These columns are isolated in this table and never loaded by normal FightLog queries.
    [MaxLength(104_857_600)]
    public string? RawFightData { get; set; }

    [MaxLength(104_857_600)]
    public string? RawHealingData { get; set; }

    [MaxLength(104_857_600)]
    public string? RawBarrierData { get; set; }
}
