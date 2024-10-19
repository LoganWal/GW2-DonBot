using Discord;
using Microsoft.Extensions.Logging;

namespace Services.Logging
{
    public class LoggingService : ILoggingService
    {
        private readonly ILogger<LoggingService> _logger;

        public LoggingService(ILogger<LoggingService> logger)
        {
            _logger = logger;
        }

        public Task LogAsync(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Debug:
                    _logger.LogDebug(msg.Message);
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation(msg.Message);
                    break;
                case LogSeverity.Warning:
                    _logger.LogWarning(msg.Message);
                    break;
                case LogSeverity.Error:
                    _logger.LogError(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Critical:
                    _logger.LogCritical(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Verbose:
                default:
                    _logger.LogInformation(msg.Message);
                    break;
            }
            return Task.CompletedTask;
        }
    }
}