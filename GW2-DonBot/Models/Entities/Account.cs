using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class Account
{
    [Key]
    public long DiscordId { get; init; }

    public decimal Points { get; set; }

    public decimal PreviousPoints { get; set; }

    public decimal AvailablePoints { get; set; }

    public DateTime? LastWvwLogDateTime { get; set; }
}