using Discord;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class FooterService(IEntityService entityService) : IFooterService
{
    private const string InviteLink = "https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot";

    public async Task<string> Generate(long guildId)
    {
        var guildQuotes = (await entityService.GuildQuote.GetWhereAsync(s => s.GuildId == guildId)).ToArray();
        return guildQuotes.Length <= 0
            ? string.Empty
            : guildQuotes[new Random().Next(0, guildQuotes.Length)].Quote.PadRight(100, ' '); // whitespace added to handle discords message width
    }

    public void AddInviteLink(EmbedBuilder builder)
    {
        builder.AddField(x =>
        {
            x.Name = "\u200B";
            x.Value = $"[Add DonBot to your server]({InviteLink})";
            x.IsInline = false;
        });
    }
}
