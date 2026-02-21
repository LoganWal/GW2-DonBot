using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class GuildWarsAccount
{
    [Key]
    public Guid GuildWarsAccountId { get; set; }

    public long DiscordId { get; init; }

    [MaxLength(1000)]
    public string? GuildWarsApiKey { get; set; }

    [MaxLength(1000)]
    public string? GuildWarsAccountName { get; set; }

    [MaxLength(1000)]
    public string? GuildWarsGuilds { get; set; }

    public int World { get; set; }

    public int FailedApiPullCount { get; set; }
}