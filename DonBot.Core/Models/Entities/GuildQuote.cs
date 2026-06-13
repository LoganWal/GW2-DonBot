using System.ComponentModel.DataAnnotations;

namespace DonBot.Core.Models.Entities;

public class GuildQuote
{
    [Key]
    public long GuildQuoteId { get; init; }

    public long GuildId { get; init; }

    [MaxLength(1000)]
    public string Quote { get; set; } = string.Empty;
}
