using System.ComponentModel.DataAnnotations;

namespace Models.Entities
{
    public class GuildWarsAccount
    {
        [Key]
        public Guid GuildWarsAccountId { get; set; }

        public long DiscordId { get; set; }

        public string? GuildWarsApiKey { get; set; }

        public string? GuildWarsAccountName { get; set; }

        public string? GuildWarsGuilds { get; set; }

        public int World { get; set; }

        public int FailedApiPullCount { get; set; }
    }
}