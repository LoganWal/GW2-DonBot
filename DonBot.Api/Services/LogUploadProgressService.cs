using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace DonBot.Api.Services;

public sealed class LogUploadProgressService : ILogUploadProgressService
{
    private readonly ConcurrentDictionary<long, Channel<string>> _channels = new();

    public void Publish(long uploadId, string stage, string message, string? dpsReportUrl = null, long? fightLogId = null)
    {
        var channel = _channels.GetOrAdd(uploadId, _ => Channel.CreateUnbounded<string>());
        var payload = JsonSerializer.Serialize(new { stage, message, dpsReportUrl, fightLogId });
        channel.Writer.TryWrite(payload);
    }

    public void Complete(long uploadId)
    {
        if (_channels.TryGetValue(uploadId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public async IAsyncEnumerable<string> Subscribe(long uploadId, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = _channels.GetOrAdd(uploadId, _ => Channel.CreateUnbounded<string>());

        await foreach (var msg in channel.Reader.ReadAllAsync(ct))
        {
            yield return msg;
        }

        _channels.TryRemove(uploadId, out _);
    }
}
