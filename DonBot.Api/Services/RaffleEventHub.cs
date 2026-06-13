using System.Threading.Channels;

namespace DonBot.Api.Services;

public sealed record RaffleStreamEvent(string Name, object Payload);

public sealed class RaffleEventSubscription : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    internal RaffleEventSubscription(Channel<RaffleStreamEvent> channel, Action dispose)
    {
        Reader = channel.Reader;
        _dispose = dispose;
    }

    public ChannelReader<RaffleStreamEvent> Reader { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispose();
    }
}

public interface IRaffleEventHub
{
    RaffleEventSubscription Subscribe(long guildId);
    void Publish(long guildId, string eventName, object payload);
}

public sealed class RaffleEventHub : IRaffleEventHub
{
    private readonly Lock _gate = new();
    private readonly Dictionary<long, List<Channel<RaffleStreamEvent>>> _subscribers = new();

    public RaffleEventSubscription Subscribe(long guildId)
    {
        var channel = Channel.CreateUnbounded<RaffleStreamEvent>();
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(guildId, out var list))
            {
                list = [];
                _subscribers[guildId] = list;
            }

            list.Add(channel);
        }

        return new RaffleEventSubscription(channel, () =>
        {
            lock (_gate)
            {
                if (_subscribers.TryGetValue(guildId, out var list))
                {
                    list.Remove(channel);
                    if (list.Count == 0)
                    {
                        _subscribers.Remove(guildId);
                    }
                }
            }

            channel.Writer.TryComplete();
        });
    }

    public void Publish(long guildId, string eventName, object payload)
    {
        List<Channel<RaffleStreamEvent>> targets;
        lock (_gate)
        {
            targets = _subscribers.TryGetValue(guildId, out var list) ? [.. list] : [];
        }

        var message = new RaffleStreamEvent(eventName, payload);
        foreach (var channel in targets)
        {
            channel.Writer.TryWrite(message);
        }
    }
}
