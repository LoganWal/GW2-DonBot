using Discord;
using DonBot.Extensions;
using DonBot.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class FooterServiceTests
{
    [Fact]
    public async Task Generate_NoQuotes_ReturnsSingleInvisibleSpacer()
    {
        var entityService = new InMemoryEntityService();
        var svc = new FooterService(entityService);

        var result = await svc.Generate(guildId: 1L);

        // Discord hides the footer icon when the text is empty, so a single Hangul-filler keeps
        // the icon visible without pushing the timestamp away from it.
        Assert.Equal("ㅤ", result);
    }

    [Fact]
    public async Task Generate_WithSingleQuote_ReturnsThatQuoteUnpadded()
    {
        var entityService = new InMemoryEntityService();
        entityService.GuildQuoteRepo.Items.Add(new GuildQuote { GuildId = 1L, Quote = "hello world" });
        var svc = new FooterService(entityService);

        var result = await svc.Generate(guildId: 1L);

        Assert.Equal("hello world", result);
    }

    [Fact]
    public async Task Generate_WithQuotes_OnlyConsidersQuotesForGivenGuild()
    {
        var entityService = new InMemoryEntityService();
        entityService.GuildQuoteRepo.Items.Add(new GuildQuote { GuildId = 1L, Quote = "guild-1-quote" });
        entityService.GuildQuoteRepo.Items.Add(new GuildQuote { GuildId = 2L, Quote = "guild-2-quote" });
        var svc = new FooterService(entityService);

        var result = await svc.Generate(guildId: 1L);

        Assert.StartsWith("guild-1-quote", result);
    }

    [Fact]
    public async Task Generate_QuoteAlreadyLongerThan100Chars_NotTruncated()
    {
        var entityService = new InMemoryEntityService();
        var longQuote = new string('x', 150);
        entityService.GuildQuoteRepo.Items.Add(new GuildQuote { GuildId = 1L, Quote = longQuote });
        var svc = new FooterService(entityService);

        var result = await svc.Generate(guildId: 1L);

        Assert.Equal(longQuote, result);
        Assert.Equal(150, result.Length);
    }

    [Fact]
    public void AddWidthSpacer_AddsInvisibleFullWidthField()
    {
        var entityService = new InMemoryEntityService();
        var svc = new FooterService(entityService);
        var builder = new EmbedBuilder().WithTitle("ignored");

        svc.AddWidthSpacer(builder);

        var field = Assert.Single(builder.Fields);
        var value = field.Value!.ToString()!;
        // Field value is a run of full-width Hangul-filler (U+3164) spacers sized to a max table row
        // so the embed renders at full width and code-block rows don't wrap on mobile.
        Assert.Equal(DiscordTable.MaxRowWidth, value.Length);
        Assert.All(value, c => Assert.Equal('ㅤ', c));
        Assert.False(field.IsInline);
    }

    [Fact]
    public void AddInviteLink_AddsFieldWithDiscordOAuthUrl()
    {
        var entityService = new InMemoryEntityService();
        var svc = new FooterService(entityService);
        var builder = new EmbedBuilder().WithTitle("ignored");

        svc.AddInviteLink(builder);

        var field = Assert.Single(builder.Fields);
        Assert.Contains("discord.com/api/oauth2/authorize", field.Value!.ToString());
        Assert.Contains("Add DonBot to your server", field.Value!.ToString());
        Assert.False(field.IsInline);
    }
}
