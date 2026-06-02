using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class Raffle
{
    [Key]
    public int Id { get; init; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public long GuildId { get; init; }

    public int RaffleType { get; init; }

    public long? CreatorDiscordId { get; set; }

    public long? MessageChannelId { get; set; }

    public long? MessageId { get; set; }
}
