namespace DonBot.Core.Models;

public static class DiscordDeliveryModes
{
    public const string GuildDefaults = "guild_defaults";
    public const string ChannelOverride = "channel_override";
}

public static class DiscordDeliveryMessageKinds
{
    public const string PveSummary = "pve-summary";
    public const string WvwSummary = "wvw-summary";
    public const string WvwAdvanced = "wvw-advanced";
    public const string WvwStream = "wvw-stream";
}

public static class DiscordDeliveryReceiptStatuses
{
    public const string Pending = "pending";
    public const string Sending = "sending";
    public const string Sent = "sent";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Ambiguous = "ambiguous";
}
