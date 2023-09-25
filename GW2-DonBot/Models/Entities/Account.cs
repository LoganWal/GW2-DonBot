using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class Account
    {
        [Key]
        public long DiscordId { get; set; }

        public string Gw2AccountId { get; set; }

        public string Gw2AccountName { get; set; }

        public string? Gw2ApiKey { get; set; }

        public decimal Points { get; set; }

        public decimal PreviousPoints { get; set; }

        public decimal AvailablePoints { get; set; }

        public DateTime? LastWvwLogDateTime { get; set; }

        public int? World { get; set; }

        public int? FailedApiPullCount { get; set; }
    }
}
