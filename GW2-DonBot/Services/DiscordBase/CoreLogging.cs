using Discord;

namespace Services.DiscordBase
{
    public class CoreLogging
    {
        public Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
