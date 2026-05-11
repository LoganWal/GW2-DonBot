using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.GuildWarsServices.MessageGeneration;
using Microsoft.Extensions.Configuration;

namespace DonBot.Tests.Services.GuildWarsServices.MessageGeneration;

// Covers the content-dedupe regression: when a fight is uploaded by user A under
// URL_A and later re-uploaded by user B under URL_B, FightLogDeduplication reuses
// the existing FightLog row (still holding URL_A). GenerateSimpleReply must therefore
// look up fights by FightLogId, not by the URLs the user posted in Discord.
public class RaidReportSimpleReplyTests
{
    private const long GuildId = 1;

    [Fact]
    public async Task GenerateSimpleReply_FightUrlInDbDiffersFromUserPostedUrl_StillReturnsFight()
    {
        var stored = MakeFight(id: 42, url: "https://b.dps.report/originalA-uploaded-by-user-a");
        var service = BuildService([stored], [MakePlayerFight(42)]);

        var (embeds, _) = await service.GenerateSimpleReply([42], GuildId);

        Assert.NotNull(embeds);
        Assert.NotEmpty(embeds!);
    }

    [Fact]
    public async Task GenerateSimpleReply_WithEmptyIdList_ReturnsNullEmbeds()
    {
        var service = BuildService([MakeFight(1, "https://b.dps.report/a")], [MakePlayerFight(1)]);

        var (embeds, webAppUrl) = await service.GenerateSimpleReply([], GuildId);

        Assert.Null(embeds);
        Assert.Null(webAppUrl);
    }

    [Fact]
    public async Task GenerateSimpleReply_WithNonExistentIds_ReturnsNullEmbeds()
    {
        var service = BuildService([MakeFight(1, "https://b.dps.report/a")], [MakePlayerFight(1)]);

        var (embeds, _) = await service.GenerateSimpleReply([999, 1000], GuildId);

        Assert.Null(embeds);
    }

    [Fact]
    public async Task GenerateSimpleReply_MixedExistingAndMissingIds_IgnoresMissing()
    {
        var fights = new List<FightLog>
        {
            MakeFight(1, "https://b.dps.report/a"),
            MakeFight(2, "https://b.dps.report/b")
        };
        var playerFights = new List<PlayerFightLog>
        {
            MakePlayerFight(fightLogId: 1, playerFightLogId: 1),
            MakePlayerFight(fightLogId: 2, playerFightLogId: 2)
        };
        var service = BuildService(fights, playerFights);

        var (embeds, _) = await service.GenerateSimpleReply([1, 999], GuildId);

        Assert.NotNull(embeds);
        Assert.NotEmpty(embeds!);
    }

    [Fact]
    public async Task GenerateSimpleReply_FightsOverviewCountReflectsResolvedIds_NotPostedUrls()
    {
        // Three fights in the DB; user posted three URLs but two were content-duplicates
        // of fight ids 10 and 20 (their stored URLs are different). The summary pipeline
        // resolves the posted URLs to ids [10, 20, 30]; the count in the Fights Overview
        // embed should be 3.
        var fights = new List<FightLog>
        {
            MakeFight(10, "https://b.dps.report/different-from-what-user-posted-1"),
            MakeFight(20, "https://b.dps.report/different-from-what-user-posted-2"),
            MakeFight(30, "https://b.dps.report/matches-what-user-posted")
        };
        var playerFights = new List<PlayerFightLog>
        {
            MakePlayerFight(fightLogId: 10, playerFightLogId: 1),
            MakePlayerFight(fightLogId: 20, playerFightLogId: 2),
            MakePlayerFight(fightLogId: 30, playerFightLogId: 3)
        };
        var service = BuildService(fights, playerFights);

        var (embeds, _) = await service.GenerateSimpleReply([10, 20, 30], GuildId);

        Assert.NotNull(embeds);
        // Fights Overview embed is the first one in the PvE path; its last column is fight count
        var fightsOverviewField = embeds![0].Fields.FirstOrDefault(f => f.Name == "Fights Overview");
        Assert.NotEqual(default, fightsOverviewField);
        // One row, three fights of the same type/mode
        Assert.Contains(" 3\n", fightsOverviewField.Value);
    }

    private static FightLog MakeFight(long id, string url) => new()
    {
        FightLogId = id,
        GuildId = GuildId,
        Url = url,
        FightType = (short)FightTypesEnum.Cairn,
        FightStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id),
        FightDurationInMs = 60_000,
        IsSuccess = true,
        FightPercent = 100,
        FightMode = 0
    };

    private static PlayerFightLog MakePlayerFight(long fightLogId, long playerFightLogId = 0) => new()
    {
        PlayerFightLogId = playerFightLogId == 0 ? fightLogId : playerFightLogId,
        FightLogId = fightLogId,
        GuildWarsAccountName = "Alice.1234",
        SubGroup = 1,
        Damage = 1000
    };

    private static RaidReportService BuildService(List<FightLog> fights, List<PlayerFightLog> playerFights)
    {
        var footer = new SequenceFooterService();
        var entityService = new FakeEntityService(fights, playerFights);
        var wvw = new FakeWvWSummaryService(footer);
        var config = new ConfigurationBuilder().Build();
        return new RaidReportService(entityService, footer, wvw, config);
    }
}
