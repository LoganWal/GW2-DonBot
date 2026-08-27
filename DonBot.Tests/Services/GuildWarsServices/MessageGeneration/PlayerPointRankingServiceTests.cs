using DonBot.Core.Models.Entities;
using DonBot.Extensions;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

public class PlayerPointRankingServiceTests
{
    private const long GuildId = 1;
    private const long FightLogId = 10;

    [Fact]
    public async Task Generate_UsesCurrentFightAwardsAndBuildsHistoricalRankingEmbeds()
    {
        var entityService = new InMemoryEntityService();
        var guild = new Guild
        {
            GuildId = GuildId,
            PlayerPointRankingsEnabled = true,
            PlayerPointRankingsChannelId = 123
        };
        await entityService.Guild.AddAsync(guild);
        await entityService.GuildQuote.AddAsync(new GuildQuote { GuildId = GuildId, Quote = "Test quote" });
        await entityService.FightLog.AddAsync(new FightLog
        {
            FightLogId = FightLogId,
            GuildId = GuildId,
            Url = "https://example.com/fight",
            FightStart = DateTime.UtcNow,
            FightDurationInMs = 60_000
        });

        await entityService.Account.AddAsync(new Account
        {
            DiscordId = 1,
            Points = 90_712.212m,
            PreviousPoints = 90
        });
        await entityService.Account.AddAsync(new Account
        {
            DiscordId = 2,
            Points = 52_347.845m,
            PreviousPoints = 70
        });
        await entityService.GuildWarsAccount.AddAsync(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(),
            DiscordId = 1,
            GuildWarsAccountName = "Alice.1234"
        });
        await entityService.GuildWarsAccount.AddAsync(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(),
            DiscordId = 2,
            GuildWarsAccountName = "Bob.5678"
        });
        await entityService.PlayerPointAward.AddAsync(Award(1, 1, "Alice.1234", 2.5m));
        await entityService.PlayerPointAward.AddAsync(Award(2, 1, "Alice.1234", 1m));
        await entityService.PlayerPointAward.AddAsync(Award(3, 2, "Bob.5678", 9m, fightLogId: 9));

        var service = new PlayerPointRankingService(entityService, new FooterService(entityService));

        var embeds = await service.Generate(guild, FightLogId);

        Assert.Equal(2, embeds.Count);
        var latest = embeds[0];
        var total = embeds[1];

        Assert.Equal(PlayerPointRankingService.EmbedTitle, latest.Title);
        Assert.Equal(PlayerPointRankingService.EmbedTitle, total.Title);
        Assert.Equal("**WvW Last fight points:**\n", latest.Description);
        Assert.Equal("**WvW total points:**\n", total.Description);
        Assert.Equal("https://example.com/fight", latest.Url);
        Assert.Equal("Test quote", latest.Footer?.Text);
        Assert.Equal("Test quote", total.Footer?.Text);

        var latestField = Assert.Single(latest.Fields.Where(field => field.Name == "Latest Fight Points"));
        Assert.Contains("Alice.1234", latestField.Value);
        Assert.Contains("+3.5", latestField.Value);
        Assert.DoesNotContain("(+3.5)", latestField.Value);
        Assert.DoesNotContain("Bob.5678", latestField.Value);

        var totalField = Assert.Single(total.Fields.Where(field => field.Name == "Total Points"));
        Assert.Contains("Alice.1234", totalField.Value);
        Assert.Contains("90,712", totalField.Value);
        Assert.Contains("Bob.5678", totalField.Value);
        Assert.Contains("52,348", totalField.Value);
        Assert.DoesNotContain("(+", totalField.Value);

        AssertTableRowsFit(latestField.Value);
        AssertTableRowsFit(totalField.Value);
    }

    [Fact]
    public void Guild_DefaultsPlayerPointRankingsToOff()
    {
        var guild = new Guild { GuildId = GuildId };

        Assert.False(guild.PlayerPointRankingsEnabled);
        Assert.Null(guild.PlayerPointRankingsChannelId);
    }

    private static PlayerPointAward Award(
        long awardId,
        long discordId,
        string accountName,
        decimal points,
        long fightLogId = FightLogId) => new()
    {
        PlayerPointAwardId = awardId,
        FightLogId = fightLogId,
        PlayerFightLogId = awardId,
        DiscordId = discordId,
        GuildWarsAccountName = accountName,
        Points = points,
        AwardedAt = DateTime.UtcNow
    };

    private static void AssertTableRowsFit(string value)
    {
        var lines = value.Replace("```", string.Empty).Split('\n').Where(line => line.Length > 0);
        Assert.All(lines, line => Assert.True(
            line.Length <= DiscordTable.MaxRowWidth,
            $"Row exceeds {DiscordTable.MaxRowWidth} chars ({line.Length}): '{line}'"));
    }
}
