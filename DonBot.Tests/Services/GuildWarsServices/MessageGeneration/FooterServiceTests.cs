using Discord;
using DonBot.Core.Models.Entities;
using DonBot.Extensions;
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

        // Discord hides footer icons when text is empty.
        Assert.Equal("ﾠ", result);
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
    public void AddWidthSpacer_NoExistingFields_AddsInvisibleFullWidthField()
    {
        var entityService = new InMemoryEntityService();
        var svc = new FooterService(entityService);
        var builder = new EmbedBuilder().WithTitle("ignored");

        svc.AddWidthSpacer(builder);

        var field = Assert.Single(builder.Fields);
        var value = field.Value!.ToString()!;
        // Halfwidth Hangul-filler keeps the embed full width on mobile.
        Assert.Equal(DiscordTable.MaxRowWidth - 3, value.Length);
        Assert.All(value, c => Assert.Equal('ﾠ', c));
        Assert.False(field.IsInline);
    }

    [Fact]
    public void AddWidthSpacer_WithExistingFields_AppendsSpacerLineToLastFieldValue()
    {
        var entityService = new InMemoryEntityService();
        var svc = new FooterService(entityService);
        var builder = new EmbedBuilder();
        builder.AddField(x => { x.Name = "Survivability Overview"; x.Value = "```table```"; x.IsInline = false; });

        svc.AddWidthSpacer(builder);

        // Appending avoids an empty name-line gap between the table and spacer.
        var field = Assert.Single(builder.Fields);
        var expectedSpacer = new string('ﾠ', DiscordTable.MaxRowWidth - 3);
        Assert.Equal($"```table```\n{expectedSpacer}", field.Value!.ToString());
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
