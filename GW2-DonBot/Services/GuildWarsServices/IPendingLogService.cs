namespace DonBot.Services.GuildWarsServices;

public record PendingLogState(List<string> Urls, long GuildId, ulong UploaderId);

public interface IPendingLogService
{
    string StorePending(PendingLogState state);
    PendingLogState? TryConsume(string key);
}
