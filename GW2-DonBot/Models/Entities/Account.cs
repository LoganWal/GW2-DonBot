using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class Account
    {
        [Key]
        public long DiscordId { get; set; }
        public string Gw2AccountId { get; set; }
        public string Gw2AccountName { get; set; }
        public string Gw2ApiKey { get; set; }
    }
}
