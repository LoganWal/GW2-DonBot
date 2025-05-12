using System.ComponentModel.DataAnnotations;

namespace DonBot.Models.Entities;

public class SteamAccount
{
    [Key]
    public long SteamId64 { get; set; }

    public long SteamId3 { get; set; }

    public long DiscordId { get; set; }
}