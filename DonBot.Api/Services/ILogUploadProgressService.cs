namespace DonBot.Api.Services;

public interface ILogUploadProgressService
{
    void Publish(long uploadId, string stage, string message, string? dpsReportUrl = null, long? fightLogId = null);
    void Complete(long uploadId);
    IAsyncEnumerable<string> Subscribe(long uploadId, CancellationToken ct);
}
