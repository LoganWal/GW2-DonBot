using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class SteamAccount
{
    [Key]
    public long SteamId64 { get; init; }

    public long SteamId3 { get; init; }

    public long DiscordId { get; init; }
}