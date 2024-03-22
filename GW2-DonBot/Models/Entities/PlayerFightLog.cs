using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class PlayerFightLog
    {
        [Key]
        public long PlayerFightLogId { get; set; }

        public long FightLogId { get; set; }

        public string GuildWarsAccountName { get; set; }

        public long Damage { get; set; }

        public decimal QuicknessDuration { get; set; }

        public decimal AlacDuration { get; set; }
    }
}
