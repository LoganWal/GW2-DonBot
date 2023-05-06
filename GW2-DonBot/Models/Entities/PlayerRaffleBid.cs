using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class PlayerRaffleBid
    {
        [Key]
        [Column(Order = 0)]
        public int RaffleId { get; set; }

        [Key]
        [Column(Order = 1)]
        public long DiscordId { get; set; }

        public decimal PointsSpent { get; set; }
    }
}