using System.Collections.Concurrent;

namespace DonBot.Services.GuildWarsServices;

public class PendingLogService : IPendingLogService
{
    private readonly ConcurrentDictionary<string, PendingLogState> _pending = new();

    public string StorePending(PendingLogState state)
    {
        var key = Guid.NewGuid().ToString("N")[..16];
        _pending[key] = state;
        return key;
    }

    public PendingLogState? TryConsume(string key)
    {
        _pending.TryRemove(key, out var state);
        return state;
    }
}
