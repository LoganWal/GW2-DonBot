using Discord;

namespace Services.Logging
{
    public interface ILoggingService
    {
        public Task Log(LogMessage msg);
    }
}
