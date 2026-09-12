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
    public async Task Generate_RanksCurrentMembersUsingOnlyAwardsFromGuildLogs()
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
        await entityService.FightLog.AddAsync(new FightLog { FightLogId = 9, GuildId = GuildId });
        await entityService.FightLog.AddAsync(new FightLog { FightLogId = 99, GuildId = 2 });

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
        await entityService.Account.AddAsync(new Account
        {
            DiscordId = 3,
            Points = 100_000m,
            PreviousPoints = 90_000m
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
        await entityService.GuildWarsAccount.AddAsync(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(),
            DiscordId = 3,
            GuildWarsAccountName = "FormerMember.9012"
        });
        await entityService.Account.AddAsync(new Account { DiscordId = 4, Points = 200_000m });
        await entityService.GuildWarsAccount.AddAsync(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(), DiscordId = 4, GuildWarsAccountName = "OtherServerOnly.1234"
        });
        await entityService.PlayerPointAward.AddAsync(Award(1, 1, "Alice.1234", 2.5m));
        await entityService.PlayerPointAward.AddAsync(Award(2, 1, "Alice.1234", 1m));
        await entityService.PlayerPointAward.AddAsync(Award(3, 2, "Bob.5678", 9m, fightLogId: 9));
        await entityService.PlayerPointAward.AddAsync(Award(4, 3, "FormerMember.9012", 10m));
        await entityService.PlayerPointAward.AddAsync(Award(5, 1, "AliceAlt.1234", 1.25m, fightLogId: 9));
        await entityService.PlayerPointAward.AddAsync(Award(6, 1, "Alice.1234", 500m, fightLogId: 99));
        await entityService.PlayerPointAward.AddAsync(Award(7, 4, "OtherServerOnly.1234", 1_000m, fightLogId: 99));

        var service = new PlayerPointRankingService(entityService, new FooterService(entityService));

        var embeds = await service.Generate(guild, FightLogId, new HashSet<long> { 1, 2, 4 });

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
        Assert.DoesNotContain("FormerMember.9012", latestField.Value);

        var totalField = Assert.Single(total.Fields.Where(field => field.Name == "Total Points"));
        Assert.Contains("Alice.1234", totalField.Value);
        Assert.Contains(DiscordTable.Row(PlayerPointRankingService.TotalPointsColumns, "002", "Alice.1234", "5"),
            totalField.Value);
        Assert.Contains("Bob.5678", totalField.Value);
        Assert.Contains(DiscordTable.Row(PlayerPointRankingService.TotalPointsColumns, "001", "Bob.5678", "9"),
            totalField.Value);
        Assert.DoesNotContain("90,712", totalField.Value);
        Assert.DoesNotContain("52,348", totalField.Value);
        Assert.DoesNotContain("OtherServerOnly.1234", totalField.Value);
        Assert.DoesNotContain("(+", totalField.Value);
        Assert.DoesNotContain("FormerMember.9012", totalField.Value);

        AssertTableRowsFit(latestField.Value);
        AssertTableRowsFit(totalField.Value);
    }

    [Fact]
    public async Task Generate_NoCurrentMembers_OmitsAllPlayers()
    {
        var entityService = new InMemoryEntityService();
        var guild = new Guild { GuildId = GuildId };
        await entityService.FightLog.AddAsync(new FightLog { FightLogId = FightLogId, GuildId = GuildId });
        await entityService.Account.AddAsync(new Account { DiscordId = 1, Points = 100m });
        await entityService.GuildWarsAccount.AddAsync(new GuildWarsAccount
        {
            GuildWarsAccountId = Guid.NewGuid(), DiscordId = 1, GuildWarsAccountName = "FormerMember.1234"
        });
        await entityService.PlayerPointAward.AddAsync(Award(1, 1, "FormerMember.1234", 10m));
        var service = new PlayerPointRankingService(entityService, new FooterService(entityService));

        var embeds = await service.Generate(guild, FightLogId, new HashSet<long>());

        Assert.Equal(2, embeds.Count);
        Assert.All(embeds, embed => Assert.DoesNotContain(embed.Fields,
            field => field.Name is "Latest Fight Points" or "Total Points"));
    }

    [Fact]
    public async Task Generate_FightFromAnotherGuild_OmitsLatestAwardsAndLink()
    {
        var entityService = new InMemoryEntityService();
        var guild = new Guild { GuildId = GuildId };
        await entityService.FightLog.AddAsync(new FightLog
        {
            FightLogId = FightLogId, GuildId = 2, Url = "https://example.com/other-server"
        });
        await entityService.PlayerPointAward.AddAsync(Award(1, 1, "Alice.1234", 10m));
        var service = new PlayerPointRankingService(entityService, new FooterService(entityService));

        var embeds = await service.Generate(guild, FightLogId, new HashSet<long> { 1 });

        Assert.Null(embeds[0].Url);
        Assert.DoesNotContain(embeds[0].Fields, field => field.Name == "Latest Fight Points");
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
