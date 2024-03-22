using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class FightLog
    {
        [Key]
        public long FightLogId { get; set; }

        public long GuildId { get; set; }

        public string Url { get; set; }

        public short FightType { get; set; }

        public DateTime FightStart { get; set; }

        public long FightDurationInMs { get; set; }

        public bool IsSuccess { get; set; }
    }
}