using Discord;

namespace DonBot.Services.Logging
{
    public interface ILoggingService
    {
        Task LogAsync(LogMessage msg);
    }
}
