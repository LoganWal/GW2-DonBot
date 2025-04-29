using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities
{
    public class FightLog
    {
        [Key]
        public long FightLogId { get; set; }

        public long GuildId { get; set; }

        [MaxLength(2000)]
        public string Url { get; set; } = string.Empty;

        public short FightType { get; set; }

        public DateTime FightStart { get; set; }

        public long FightDurationInMs { get; set; }

        public bool IsSuccess { get; set; }

        public decimal FightPercent { get; set; }

        public int? FightPhase { get; set; }
    }
}