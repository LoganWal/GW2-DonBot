using Discord;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.LoggingServices;

public sealed class LoggingService(ILogger<LoggingService> logger) : ILoggingService
{
    public Task LogAsync(LogMessage msg)
    {
        var message = msg.Message
            ?? msg.Exception?.Message
            ?? $"Discord.Net {msg.Severity} log with no message from {msg.Source}";

        switch (msg.Severity)
        {
            case LogSeverity.Debug:
                logger.LogDebug(message);
                break;
            case LogSeverity.Info:
                logger.LogInformation(message);
                break;
            case LogSeverity.Warning:
                logger.LogWarning(message);
                break;
            case LogSeverity.Error:
                logger.LogError(msg.Exception, message);
                break;
            case LogSeverity.Critical:
                logger.LogCritical(msg.Exception, message);
                break;
            case LogSeverity.Verbose:
            default:
                logger.LogInformation(message);
                break;
        }
        return Task.CompletedTask;
    }
}
