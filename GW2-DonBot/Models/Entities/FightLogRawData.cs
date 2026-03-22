using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class FightLogRawData
{
    [Key]
    public long FightLogId { get; init; }

    // Raw EliteInsights JSON blobs can be several MB — MaxLength set to 100 MB ceiling.
    // These columns are isolated in this table and never loaded by normal FightLog queries.
    // Private getters are used by EF Core internally when generating INSERT/UPDATE parameters.
    [MaxLength(104_857_600)]
    public string? RawFightData { private get; set; }

    [MaxLength(104_857_600)]
    public string? RawHealingData { private get; set; }

    [MaxLength(104_857_600)]
    public string? RawBarrierData { private get; set; }
}
