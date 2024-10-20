using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities
{
    public class FightsReport
    {
        [Key]
        public long FightsReportId { get; set; }

        public long GuildId { get; set; }

        public DateTime FightsStart { get; set; }

        public DateTime? FightsEnd { get; set; }
    }
}