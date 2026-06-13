using System.ComponentModel.DataAnnotations;

namespace DonBot.Core.Models.Entities;

public class FightLogRawData
{
    [Key]
    public long FightLogId { get; init; }

    // Large raw EI blobs live here so normal FightLog queries do not load them.
    [MaxLength(104_857_600)]
    public string? RawFightData { get; set; }

    [MaxLength(104_857_600)]
    public string? RawHealingData { get; set; }

    [MaxLength(104_857_600)]
    public string? RawBarrierData { get; set; }
}
