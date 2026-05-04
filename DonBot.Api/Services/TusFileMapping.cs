using System.Collections.Concurrent;

namespace DonBot.Api.Services;

public sealed class TusFileMapping
{
    private readonly ConcurrentDictionary<string, long> _map = new();

    public void Add(string tusFileId, long logUploadId) => _map[tusFileId] = logUploadId;

    public bool TryRemove(string tusFileId, out long logUploadId) =>
        _map.TryRemove(tusFileId, out logUploadId);
}
