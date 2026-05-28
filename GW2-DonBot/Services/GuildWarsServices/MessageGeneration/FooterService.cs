using Discord;
using DonBot.Extensions;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class FooterService(IEntityService entityService) : IFooterService
{
    private const string InviteLink = "https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot";

    // Hangul Filler (U+3164): renders as a truly blank glyph with width, and unlike a regular space
    // Discord does NOT trim it from the end of a line. Used to force an embed's width — see
    // AddWidthSpacer.
    private const char WidthSpacer = 'ㅤ';

    // The spacer only needs to be as wide as the widest table row it's protecting. Tables are capped
    // at DiscordTable.MaxRowWidth monospace chars, but the spacer glyph is full-width, so fewer are
    // needed to reach the embed's content width — trimmed by 5 so it tracks the table without
    // overshooting.
    private const int SpacerWidth = DiscordTable.MaxRowWidth - 5;

    public async Task<string> Generate(long guildId)
    {
        var guildQuotes = (await entityService.GuildQuote.GetWhereAsync(s => s.GuildId == guildId)).ToArray();
        // Discord hides the footer icon when the footer text is empty, so fall back to a single
        // invisible Hangul-filler when there's no quote to keep the icon next to the timestamp.
        return guildQuotes.Length <= 0
            ? WidthSpacer.ToString()
            : guildQuotes[Random.Shared.Next(0, guildQuotes.Length)].Quote;
    }

    // A code block inside an embed wraps to the embed's content width, and only proportional-font
    // lines (title/description/fields) set that width — the code block itself never widens the
    // embed. So on mobile a wide table row wraps its last column onto a second line. Adding a field
    // whose value is a line of invisible width-spacers forces the embed to its full width, keeping
    // table rows on one line. This lives in a field (not the footer) so it doesn't push the footer
    // timestamp away from the bot icon.
    public void AddWidthSpacer(EmbedBuilder builder)
    {
        builder.AddField(x =>
        {
            x.Name = "​";
            x.Value = new string(WidthSpacer, SpacerWidth);
            x.IsInline = false;
        });
    }

    public void AddInviteLink(EmbedBuilder builder)
    {
        builder.AddField(x =>
        {
            x.Name = "​";
            x.Value = $"[Add DonBot to your server]({InviteLink})";
            x.IsInline = false;
        });
    }
}
