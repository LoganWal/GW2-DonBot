using Models.Entities;

namespace Handlers.MessageGenerationHandlers
{
    public class FooterHandler
    {
        private readonly DatabaseContext _databaseContext;

        public FooterHandler(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        public string Generate(long guildId)
        {
            var guildQuotes = _databaseContext.GuildQuote.Where(s => s.GuildId == guildId).ToArray();
            return guildQuotes.Length <= 0 
                ? string.Empty 
                : guildQuotes[new Random().Next(0, guildQuotes.Length)].Quote.PadRight(100, ' '); // whitespace added to handle discords message width
        }
    }
}
