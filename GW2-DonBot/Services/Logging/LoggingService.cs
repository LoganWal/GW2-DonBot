using Discord;

namespace Services.Logging
{
    public class LoggingService : ILoggingService
    {
        public Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
