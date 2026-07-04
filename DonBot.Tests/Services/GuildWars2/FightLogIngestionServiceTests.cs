using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;
using DonBot.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DonBot.Tests.Services.GuildWars2;

public class FightLogIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_NewPveLog_CreatesFightRawPlayerAndMechanicRows()
    {
        using var db = new SqliteTestDb();
        var service = new FightLogIngestionService(db.Factory);
        var data = BuildPveData("https://dps.report/new", rawFightData: "raw-new");
        var fightPhase = FightLogMaterializer.ResolveFightPhase(data);
        var players = new[]
        {
            new Gw2Player
            {
                AccountName = "Player.1234",
                CharacterName = "Character",
                Damage = 600_000,
                Mechanics = new Dictionary<string, long> { ["Spread"] = 2 }
            }
        };

        var result = await service.IngestAsync(new FightLogIngestionRequest(data, fightPhase, players)
        {
            GuildId = 42
        });

        Assert.True(result.Created);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var fightLog = Assert.Single(await ctx.FightLog.ToListAsync());
        Assert.Equal(result.FightLogId, fightLog.FightLogId);
        Assert.Equal(42, fightLog.GuildId);
        Assert.Equal((short)FightTypesEnum.Spirit, fightLog.FightType);
        Assert.Equal(60_000, fightLog.FightDurationInMs);
        Assert.Equal("dps.report", fightLog.Source);

        var raw = Assert.Single(await ctx.FightLogRawData.ToListAsync());
        Assert.Equal(fightLog.FightLogId, raw.FightLogId);
        Assert.Equal("raw-new", raw.RawFightData);

        var playerLog = Assert.Single(await ctx.PlayerFightLog.ToListAsync());
        Assert.Equal(fightLog.FightLogId, playerLog.FightLogId);
        Assert.Equal("Player.1234", playerLog.GuildWarsAccountName);

        var mechanic = Assert.Single(await ctx.PlayerFightLogMechanic.ToListAsync());
        Assert.Equal(playerLog.PlayerFightLogId, mechanic.PlayerFightLogId);
        Assert.Equal("Spread", mechanic.MechanicName);
        Assert.Equal(2, mechanic.MechanicCount);
    }

    [Fact]
    public async Task IngestAsync_ExistingLogWithAttachMode_AttachesGuildAndUpsertsRawOnly()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.FightLog.Add(new FightLog
            {
                FightLogId = 1,
                GuildId = 0,
                Url = "https://dps.report/existing",
                FightType = (short)FightTypesEnum.Vale,
                FightStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FightDurationInMs = 1,
                Source = "seed"
            });
            seed.FightLogRawData.Add(new FightLogRawData { FightLogId = 1, RawFightData = "old" });
            await seed.SaveChangesAsync();
        }

        var service = new FightLogIngestionService(db.Factory);
        var data = BuildPveData("https://dps.report/existing", rawFightData: "new-raw");
        var result = await service.IngestAsync(new FightLogIngestionRequest(data, FightLogMaterializer.ResolveFightPhase(data), [])
        {
            GuildId = 99,
            ExistingLogUpdateMode = ExistingFightLogUpdateMode.AttachGuildAndRawData,
            SourceFallback = "upload"
        });

        Assert.False(result.Created);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var fightLog = Assert.Single(await ctx.FightLog.ToListAsync());
        Assert.Equal(99, fightLog.GuildId);
        Assert.Equal((short)FightTypesEnum.Vale, fightLog.FightType);
        Assert.Equal(1, fightLog.FightDurationInMs);
        Assert.Equal("seed", fightLog.Source);
        Assert.Empty(ctx.PlayerFightLog);
        Assert.Equal("new-raw", Assert.Single(ctx.FightLogRawData).RawFightData);
    }

    [Fact]
    public async Task IngestAsync_ExistingLogWithAttachMode_DoesNotRefreshPlayerRows()
    {
        using var db = new SqliteTestDb();
        long playerFightLogId;
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.FightLog.Add(new FightLog
            {
                FightLogId = 1,
                GuildId = 0,
                Url = "https://dps.report/existing",
                FightType = (short)FightTypesEnum.Vale,
                FightStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FightDurationInMs = 1,
                Source = "seed"
            });
            await seed.SaveChangesAsync();

            var stalePlayerLog = new PlayerFightLog
            {
                FightLogId = 1,
                GuildWarsAccountName = "Player.1234",
                CharacterName = "OldCharacter",
                Damage = 1
            };
            seed.PlayerFightLog.Add(stalePlayerLog);
            await seed.SaveChangesAsync();

            playerFightLogId = stalePlayerLog.PlayerFightLogId;
            seed.PlayerFightLogMechanic.Add(new PlayerFightLogMechanic
            {
                PlayerFightLogId = playerFightLogId,
                MechanicName = "OldMechanic",
                MechanicCount = 99
            });
            await seed.SaveChangesAsync();
        }

        var service = new FightLogIngestionService(db.Factory);
        var data = BuildPveData("https://dps.report/existing", rawFightData: "new-raw");
        var players = new[]
        {
            new Gw2Player
            {
                AccountName = "Player.1234",
                CharacterName = "Character",
                Damage = 600_000,
                Mechanics = new Dictionary<string, long> { ["Spread"] = 2 }
            }
        };

        var result = await service.IngestAsync(new FightLogIngestionRequest(data, FightLogMaterializer.ResolveFightPhase(data), players)
        {
            GuildId = 99,
            ExistingLogUpdateMode = ExistingFightLogUpdateMode.AttachGuildAndRawData
        });

        Assert.False(result.Created);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var playerLog = Assert.Single(await ctx.PlayerFightLog.ToListAsync());
        Assert.Equal(playerFightLogId, playerLog.PlayerFightLogId);
        Assert.Equal("OldCharacter", playerLog.CharacterName);
        Assert.Equal(1, playerLog.Damage);

        var mechanic = Assert.Single(await ctx.PlayerFightLogMechanic.ToListAsync());
        Assert.Equal(playerFightLogId, mechanic.PlayerFightLogId);
        Assert.Equal("OldMechanic", mechanic.MechanicName);
        Assert.Equal(99, mechanic.MechanicCount);

        var fightLog = Assert.Single(await ctx.FightLog.ToListAsync());
        Assert.Equal(99, fightLog.GuildId);
        Assert.Equal("new-raw", Assert.Single(ctx.FightLogRawData).RawFightData);
    }

    [Fact]
    public async Task IngestAsync_ExistingLogWithRefreshMode_UpdatesPlayerRowsAndRefreshesMechanics()
    {
        using var db = new SqliteTestDb();
        long playerFightLogId;
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.FightLog.Add(new FightLog
            {
                FightLogId = 1,
                GuildId = 0,
                Url = "https://dps.report/existing",
                FightType = (short)FightTypesEnum.Vale,
                FightStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FightDurationInMs = 1,
                Source = "seed"
            });
            await seed.SaveChangesAsync();

            var stalePlayerLog = new PlayerFightLog
            {
                FightLogId = 1,
                GuildWarsAccountName = "Player.1234",
                CharacterName = "OldCharacter",
                Damage = 1
            };
            seed.PlayerFightLog.Add(stalePlayerLog);
            await seed.SaveChangesAsync();

            playerFightLogId = stalePlayerLog.PlayerFightLogId;
            seed.PlayerFightLogMechanic.Add(new PlayerFightLogMechanic
            {
                PlayerFightLogId = playerFightLogId,
                MechanicName = "OldMechanic",
                MechanicCount = 99
            });
            seed.PlayerPointAward.Add(new PlayerPointAward
            {
                FightLogId = 1,
                PlayerFightLogId = playerFightLogId,
                DiscordId = 123,
                GuildWarsAccountName = "Player.1234",
                FightType = (short)FightTypesEnum.Vale,
                Metric = "dps",
                MetricLabel = "DPS",
                AwardedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            await seed.SaveChangesAsync();
        }

        var service = new FightLogIngestionService(db.Factory);
        var data = BuildPveData("https://dps.report/existing", rawFightData: "new-raw");
        var players = new[]
        {
            new Gw2Player
            {
                AccountName = "Player.1234",
                CharacterName = "Character",
                Damage = 600_000,
                Mechanics = new Dictionary<string, long> { ["Spread"] = 2 }
            }
        };

        var result = await service.IngestAsync(new FightLogIngestionRequest(data, FightLogMaterializer.ResolveFightPhase(data), players)
        {
            GuildId = 99,
            ExistingLogUpdateMode = ExistingFightLogUpdateMode.RefreshMetadataAndRawData
        });

        Assert.False(result.Created);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var playerLog = Assert.Single(await ctx.PlayerFightLog.ToListAsync());
        Assert.Equal(playerFightLogId, playerLog.PlayerFightLogId);
        Assert.Equal("Character", playerLog.CharacterName);
        Assert.Equal(600_000, playerLog.Damage);

        var mechanic = Assert.Single(await ctx.PlayerFightLogMechanic.ToListAsync());
        Assert.Equal(playerFightLogId, mechanic.PlayerFightLogId);
        Assert.Equal("Spread", mechanic.MechanicName);
        Assert.Equal(2, mechanic.MechanicCount);

        var award = Assert.Single(await ctx.PlayerPointAward.ToListAsync());
        Assert.Equal(playerFightLogId, award.PlayerFightLogId);
        Assert.Equal("new-raw", Assert.Single(ctx.FightLogRawData).RawFightData);
    }

    [Fact]
    public async Task IngestAsync_ExistingLogWithRefreshMode_RefreshesMetadataAndRawOnly()
    {
        using var db = new SqliteTestDb();
        await using (var seed = await db.Factory.CreateDbContextAsync())
        {
            seed.FightLog.Add(new FightLog
            {
                FightLogId = 1,
                GuildId = 1,
                Url = "https://dps.report/existing",
                FightType = (short)FightTypesEnum.Vale,
                FightStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FightDurationInMs = 1,
                Source = "seed"
            });
            await seed.SaveChangesAsync();
        }

        var service = new FightLogIngestionService(db.Factory);
        var data = BuildPveData("https://dps.report/existing", rawFightData: "new-raw");
        var result = await service.IngestAsync(new FightLogIngestionRequest(data, FightLogMaterializer.ResolveFightPhase(data), [])
        {
            GuildId = 99,
            ExistingLogUpdateMode = ExistingFightLogUpdateMode.RefreshMetadataAndRawData
        });

        Assert.False(result.Created);
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var fightLog = Assert.Single(await ctx.FightLog.ToListAsync());
        Assert.Equal(99, fightLog.GuildId);
        Assert.Equal((short)FightTypesEnum.Spirit, fightLog.FightType);
        Assert.Equal(60_000, fightLog.FightDurationInMs);
        Assert.Equal("dps.report", fightLog.Source);
        Assert.Empty(ctx.PlayerFightLog);
        Assert.Equal("new-raw", Assert.Single(ctx.FightLogRawData).RawFightData);
    }

    private static EliteInsightDataModel BuildPveData(string url, string rawFightData) =>
        new(
            new FightEliteInsightDataModel
            {
                Url = url,
                EncounterId = 131332,
                EncounterStart = "2026-06-29 10:00:00 +00:00",
                Success = true,
                Targets = [new ArcDpsTarget { Percent = 50f, Health = 100 }],
                Phases =
                [
                    new ArcDpsPhase
                    {
                        Duration = 60_000,
                        Success = true,
                        EncounterDuration = "00:01:00.000"
                    }
                ]
            },
            new HealingEliteInsightDataModel(),
            new BarrierEliteInsightDataModel(),
            rawFightData,
            null,
            null);
}
