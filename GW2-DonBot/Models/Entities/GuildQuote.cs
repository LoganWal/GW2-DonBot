using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class GuildQuote
{
    [Key]
    public long GuildQuoteId { get; init; }

    public long GuildId { get; init; }

    [MaxLength(1000)]
    public string Quote { get; init; } = string.Empty;
}