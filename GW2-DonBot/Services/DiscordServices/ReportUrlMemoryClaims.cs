using System.Collections.Concurrent;

namespace DonBot.Services.DiscordServices;

internal sealed class ReportUrlMemoryClaims
{
    private readonly ConcurrentDictionary<string, byte> _claims = new(StringComparer.Ordinal);

    public bool TryClaim(string reportUrl) => _claims.TryAdd(reportUrl, 0);

    public void Release(string reportUrl) => _claims.TryRemove(reportUrl, out _);
}
