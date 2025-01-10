using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities
{
    public class GuildQuote
    {
        [Key]
        public long GuildQuoteId { get; set; }

        public long GuildId { get; set; }

        [MaxLength(1000)]
        public string Quote { get; set; } = string.Empty;
    }
}