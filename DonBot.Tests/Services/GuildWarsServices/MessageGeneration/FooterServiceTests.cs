using Discord;
using DonBot.Models.Entities;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class FooterServiceTests
{
    [Fact]
    public async Task Generate_NoQuotes_ReturnsEmptyString()
    {
        var entityService = new InMemoryEntityService();
        var svc = new FooterService(entityService);

        var result = await svc.Generate(guildId: 1L);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task Generate_WithSingleQuote_ReturnsThatQuotePaddedTo100Chars()
    {
        var entityService = new InMemoryEntityService();
        entityService.GuildQuoteRepo.Items.Add(new GuildQuote { GuildId = 1L, Quote = "hello world" });
        var svc = new FooterService(entityService);

        var result = await svc.Generate(guildId: 1L);

        Assert.StartsWith("hello world", result);
        Assert.Equal(100, result.Length);
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
