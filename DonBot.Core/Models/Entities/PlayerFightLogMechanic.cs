using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class PlayerFightLogMechanic
{
    [Key]
    public long PlayerFightLogMechanicId { get; init; }

    public long PlayerFightLogId { get; init; }

    [MaxLength(500)]
    public string MechanicName { get; init; } = string.Empty;

    public long MechanicCount { get; init; }
}
