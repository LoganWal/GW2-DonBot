using Discord;
using DonBot.Extensions;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class FooterService(IEntityService entityService) : IFooterService
{
    private const string InviteLink = "https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot";

    // Halfwidth Hangul Filler (U+FFA0): renders as a blank glyph with width, and unlike a regular
    // space Discord does NOT trim it from the end of a line. It is also not whitespace per .NET, so
    // it passes Discord.Net's "field value must not be entirely whitespace" check. Used to force an
    // embed's width, see AddWidthSpacer. (Hangul Filler U+3164 was used previously but Discord
    // stopped honouring its width, so the spacer collapsed to blank lines and rows wrapped again.)
    private const char WidthSpacer = 'ﾠ';

    // The spacer only needs to be as wide as the widest table row it's protecting. Tables are capped
    // at DiscordTable.MaxRowWidth monospace chars, but the spacer glyph is full-width, so fewer are
    // needed to reach the embed's content width. Trimmed by 3 so the spacer line itself doesn't wrap
    // onto a second line.
    private const int SpacerWidth = DiscordTable.MaxRowWidth - 3;

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
    // embed. So on mobile a wide table row wraps its last column onto a second line. A line of
    // invisible width-spacers forces the embed to its full width, keeping table rows on one line.
    //
    // We append the spacer onto the last field's value (on its own line, just after the closing code
    // fence) rather than adding a new field: a separate field renders an empty name-line above its
    // value, which shows as a blank gap between the table and the spacer. Reusing the last field
    // avoids that gap. If the embed has no fields yet, fall back to adding one.
    public void AddWidthSpacer(EmbedBuilder builder)
    {
        var spacer = new string(WidthSpacer, SpacerWidth);

        if (builder.Fields.Count > 0)
        {
            var lastField = builder.Fields[^1];
            lastField.Value = $"{lastField.Value}\n{spacer}";
            return;
        }

        builder.AddField(x =>
        {
            x.Name = "​";
            x.Value = spacer;
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
