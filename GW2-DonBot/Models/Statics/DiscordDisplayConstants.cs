namespace DonBot.Models.Statics;

internal static class DiscordDisplayConstants
{
    // Kept narrow so per-player rows (index + name + values) stay within DiscordTable.MaxRowWidth
    // and Discord doesn't wrap the trailing column onto its own line on mobile.
    public const int PlayerNameWidth = 20;
}
