using DonBot.Models.Entities;

namespace DonBot.Handlers
{
    public class FooterHandler(DatabaseContext databaseContext)
    {
        public string Generate(long guildId)
        {
            var guildQuotes = databaseContext.GuildQuote.Where(s => s.GuildId == guildId).ToArray();
            return guildQuotes.Length <= 0
                ? string.Empty
                : guildQuotes[new Random().Next(0, guildQuotes.Length)].Quote.PadRight(100, ' '); // whitespace added to handle discords message width
        }
    }
}
