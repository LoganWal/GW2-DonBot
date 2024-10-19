using Discord;

namespace Services.Logging
{
    public interface ILoggingService
    {
        Task LogAsync(LogMessage msg);
    }
}
