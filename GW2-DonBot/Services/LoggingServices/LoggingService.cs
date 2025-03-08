using Discord;
using Microsoft.Extensions.Logging;

namespace DonBot.Services.LoggingServices
{
    public class LoggingService(ILogger<LoggingService> logger) : ILoggingService
    {
        public Task LogAsync(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Debug:
                    logger.LogDebug(msg.Message);
                    break;
                case LogSeverity.Info:
                    logger.LogInformation(msg.Message);
                    break;
                case LogSeverity.Warning:
                    logger.LogWarning(msg.Message);
                    break;
                case LogSeverity.Error:
                    logger.LogError(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Critical:
                    logger.LogCritical(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Verbose:
                default:
                    logger.LogInformation(msg.Message);
                    break;
            }
            return Task.CompletedTask;
        }
    }
}