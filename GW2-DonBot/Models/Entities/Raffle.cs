using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class Raffle
{
    [Key]
    public int Id { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public long GuildId { get; set; }

    public int RaffleType { get; set; }
}