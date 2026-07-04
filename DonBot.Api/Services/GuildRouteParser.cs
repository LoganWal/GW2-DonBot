namespace DonBot.Api.Services;

public readonly record struct GuildRouteId(long Value, ulong UnsignedValue);

public static class GuildRouteParser
{
    public static bool TryParse(string? guildId, out GuildRouteId parsed)
    {
        parsed = default;
        if (!ulong.TryParse(guildId, out var unsignedValue)
            || unsignedValue == 0
            || unsignedValue > long.MaxValue)
        {
            return false;
        }

        parsed = new GuildRouteId((long)unsignedValue, unsignedValue);
        return true;
    }

    public static bool TryNormalize(long guildId, out GuildRouteId parsed)
    {
        parsed = default;
        if (guildId <= 0)
        {
            return false;
        }

        parsed = new GuildRouteId(guildId, (ulong)guildId);
        return true;
    }
}
