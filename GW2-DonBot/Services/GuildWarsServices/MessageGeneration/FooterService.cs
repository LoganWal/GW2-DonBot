using Discord;
using DonBot.Extensions;
using DonBot.Services.DatabaseServices;

namespace DonBot.Services.GuildWarsServices.MessageGeneration;

public sealed class FooterService(IEntityService entityService) : IFooterService
{
    private const string InviteLink = "https://discord.com/api/oauth2/authorize?client_id=1021682849797111838&permissions=8&scope=bot";

    // U+FFA0 renders as a blank, non-whitespace glyph that Discord keeps at line ends.
    private const char WidthSpacer = 'ﾠ';

    // U+FFA0 is wider than monospace table chars, so trim the spacer below MaxRowWidth.
    private const int SpacerWidth = DiscordTable.MaxRowWidth - 3;

    public async Task<string> Generate(long guildId)
    {
        var guildQuotes = (await entityService.GuildQuote.GetWhereAsync(s => s.GuildId == guildId)).ToArray();
        // Discord hides the footer icon when footer text is empty.
        return guildQuotes.Length <= 0
            ? WidthSpacer.ToString()
            : guildQuotes[Random.Shared.Next(0, guildQuotes.Length)].Quote;
    }

    // Code blocks do not widen embeds on mobile, so append an invisible spacer line
    // to the last field instead of adding a visible blank field gap.
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
