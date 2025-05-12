using Discord;

namespace DonBot.Services.LoggingServices;

public interface ILoggingService
{
    Task LogAsync(LogMessage msg);
}