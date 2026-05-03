using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DonBot.Models.Entities;

public class PlayerRaffleBid
{
    [Key]
    [Column(Order = 0)]
    public int RaffleId { get; init; }

    [Key]
    [Column(Order = 1)]
    public long DiscordId { get; init; }

    public decimal PointsSpent { get; set; }
}