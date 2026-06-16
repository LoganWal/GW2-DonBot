using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using DonBot.Tests.Infrastructure;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

// Covers leaderboard embed shells without pinning exact numbers.
public class WeeklyLeaderboardServiceTests
{
    private const long GuildId = 1;
    private const string Quote = "Test quote";

    [Fact]
    public async Task GeneratePvE_WithData_ReturnsEmbedWithAllSections()
    {
        var entityService = SeededEntityService(out var guild);
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        var embed = await svc.GeneratePvE(guild);

        Assert.NotNull(embed);
        Assert.StartsWith("PvE Weekly Leaderboard", embed.Title);
        Assert.Contains("Week of", embed.Description);
        var fieldNames = embed.Fields.Select(f => f.Name).ToList();
        Assert.Contains("DPS - DPS", fieldNames);
        Assert.Contains("Cleave - DPS", fieldNames);
        Assert.Contains("Res Time", fieldNames);
        Assert.Contains("Damage Taken", fieldNames);
        Assert.Contains("Times Downed", fieldNames);
        Assert.Equal(Quote, embed.Footer?.Text);
    }

    [Fact]
    public async Task GeneratePvE_NoFights_ReturnsNull()
    {
        var entityService = new InMemoryEntityService();
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        var embed = await svc.GeneratePvE(new Guild { GuildId = GuildId });

        Assert.Null(embed);
    }

    [Fact]
    public async Task GenerateWvW_WithData_ReturnsTwoEmbedsWithExpectedSections()
    {
        var entityService = SeededEntityService(out var guild);
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        var embeds = await svc.GenerateWvW(guild);

        Assert.NotNull(embeds);
        Assert.Equal(2, embeds.Count);

        Assert.StartsWith("WvW Weekly Leaderboard", embeds[0].Title);
        var firstFields = embeds[0].Fields.Select(f => f.Name).ToList();
        Assert.Contains("Damage - DPS", firstFields);
        Assert.Contains("Cleanses", firstFields);
        Assert.Contains("Strips", firstFields);
        Assert.Contains("Stab", firstFields);
        Assert.Contains("Healing", firstFields);

        Assert.Contains("Advanced", embeds[1].Title);
        var secondFields = embeds[1].Fields.Select(f => f.Name).ToList();
        Assert.Contains("Barrier", secondFields);
        Assert.Contains("Times Downed", secondFields);
        Assert.Contains("Damage Taken", secondFields);
        Assert.Contains("Kills", secondFields);
        Assert.Contains("Distance From Tag", secondFields);

        Assert.Equal(Quote, embeds[0].Footer?.Text);
        Assert.Equal(Quote, embeds[1].Footer?.Text);
    }

    [Fact]
    public async Task GenerateWvW_NoFights_ReturnsNull()
    {
        var entityService = new InMemoryEntityService();
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        var embeds = await svc.GenerateWvW(new Guild { GuildId = GuildId });

        Assert.Null(embeds);
    }

    [Fact]
    public async Task GetPlayerRanks_BothEnabledWithData_HasWvWAndPveSectionsAndRanksThePlayer()
    {
        var entityService = SeededEntityService(out var guild);
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        // Alice is seeded with higher damage, so rank order is deterministic.
        var embed = await svc.GetPlayerRanks(guild, ["Alice.1234"]);

        Assert.NotNull(embed);
        Assert.StartsWith("Your Weekly Rankings", embed.Title);
        var fieldNames = embed.Fields.Select(f => f.Name).ToList();
        Assert.Contains("WvW", fieldNames);
        Assert.Contains("PvE", fieldNames);

        var wvw = embed.Fields.First(f => f.Name == "WvW").Value;
        Assert.Contains("Damage", wvw);
        Assert.Contains("#1/", wvw);
        Assert.Equal(Quote, embed.Footer?.Text);
    }

    [Fact]
    public async Task GetPlayerRanks_FlagsDisabled_ReturnsNull()
    {
        var entityService = SeededEntityService(out _);
        var guild = new Guild { GuildId = GuildId, WvwLeaderboardEnabled = false, PveLeaderboardEnabled = false };
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        var embed = await svc.GetPlayerRanks(guild, ["Alice.1234"]);

        Assert.Null(embed);
    }

    [Fact]
    public async Task GetPlayerRanks_EnabledButNoData_ReturnsNull()
    {
        var entityService = new InMemoryEntityService();
        var guild = new Guild { GuildId = GuildId, WvwLeaderboardEnabled = true, PveLeaderboardEnabled = true };
        var svc = new WeeklyLeaderboardService(entityService, new FooterService(entityService));

        var embed = await svc.GetPlayerRanks(guild, ["Alice.1234"]);

        Assert.Null(embed);
    }

    // PvE player-rank eligibility needs at least six fights per player.
    private static InMemoryEntityService SeededEntityService(out Guild guild)
    {
        var entityService = new InMemoryEntityService();
        guild = new Guild { GuildId = GuildId, WvwLeaderboardEnabled = true, PveLeaderboardEnabled = true };
        entityService.GuildRepo.Items.Add(guild);
        entityService.GuildQuoteRepo.Items.Add(new GuildQuote { GuildId = GuildId, Quote = Quote });

        var recent = DateTime.UtcNow.AddDays(-1);
        var nextFightId = 1L;
        var nextPlayerFightId = 1L;

        void AddFight(FightTypesEnum type)
        {
            var fight = new FightLog
            {
                FightLogId = nextFightId++,
                GuildId = GuildId,
                Url = $"https://example.com/{nextFightId}",
                FightType = (short)type,
                FightStart = recent,
                FightDurationInMs = 60_000,
                IsSuccess = true,
                FightPercent = 100,
                FightMode = 0,
            };
            entityService.FightLogRepo.Items.Add(fight);

            entityService.PlayerFightLogRepo.Items.Add(MakePlayerFight(fight.FightLogId, "Alice.1234", high: true));
            entityService.PlayerFightLogRepo.Items.Add(MakePlayerFight(fight.FightLogId, "Bob.5678", high: false));
        }

        PlayerFightLog MakePlayerFight(long fightLogId, string account, bool high) => new()
        {
            PlayerFightLogId = nextPlayerFightId++,
            FightLogId = fightLogId,
            GuildWarsAccountName = account,
            SubGroup = 1,
            Damage = high ? 2_000_000 : 1_000_000,
            Cleave = high ? 500_000 : 250_000,
            DamageDownContribution = high ? 50_000 : 25_000,
            Cleanses = high ? 400 : 200,
            Strips = high ? 120 : 60,
            Healing = high ? 800_000 : 400_000,
            BarrierGenerated = high ? 300_000 : 150_000,
            Kills = high ? 40 : 20,
            TimesDowned = high ? 1 : 3,
            DamageTaken = high ? 500_000 : 900_000,
            ResurrectionTime = high ? 2_000 : 8_000,
            StabGenOnGroup = high ? 5 : 2,
            DistanceFromTag = high ? 300 : 700,
        };

        AddFight(FightTypesEnum.WvW);
        for (var i = 0; i < 6; i++)
        {
            AddFight(FightTypesEnum.Cairn);
        }

        return entityService;
    }
}
