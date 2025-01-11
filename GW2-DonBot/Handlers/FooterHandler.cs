using DonBot.Services.DatabaseServices;

namespace DonBot.Handlers
{
    public class FooterHandler(IEntityService entityService)
    {
        public async Task<string> Generate(long guildId)
        {
            var guildQuotes = (await entityService.GuildQuote.GetWhereAsync(s => s.GuildId == guildId)).ToArray();
            return guildQuotes.Length <= 0
                ? string.Empty
                : guildQuotes[new Random().Next(0, guildQuotes.Length)].Quote.PadRight(100, ' '); // whitespace added to handle discords message width
        }
    }
}
