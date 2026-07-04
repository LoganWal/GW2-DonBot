using DonBot.Core.Models.GuildWars2;
using DonBot.Core.Services.GuildWars2;

namespace DonBot.Tests.Services.GuildWars2;

public class DpsReportGetJsonMapperTests
{
    [Fact]
    public void Map_MinimalValidPayload_ReturnsLegacyModelAndNormalizedRawFightData()
    {
        var result = DpsReportGetJsonMapper.Map(
            """
            {
              "fightName": "Minimal Fight",
              "success": true,
              "players": [],
              "targets": [],
              "phases": []
            }
            """,
            "https://dps.report/minimal");

        Assert.Equal("https://dps.report/minimal", result.FightEliteInsightDataModel.Url);
        Assert.Equal("Minimal Fight", result.FightEliteInsightDataModel.LogName);
        Assert.True(result.FightEliteInsightDataModel.Success);
        Assert.Empty(result.FightEliteInsightDataModel.Players!);
        Assert.Empty(result.FightEliteInsightDataModel.Targets!);
        Assert.Empty(result.FightEliteInsightDataModel.Phases!);
        Assert.NotNull(result.RawFightData);
        Assert.Contains("Minimal Fight", result.RawFightData);
        Assert.Null(result.RawHealingData);
        Assert.Null(result.RawBarrierData);
    }

    [Fact]
    public void Map_ErrorPayload_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DpsReportGetJsonMapper.Map("""{"error":"bad permalink"}""", "https://dps.report/bad"));

        Assert.Equal("getJson response did not contain Elite Insights log data.", ex.Message);
    }

    [Fact]
    public void Map_StatsAndExtensions_PopulatesLegacyVectorsAtSharedIndices()
    {
        var result = DpsReportGetJsonMapper.Map(
            """
            {
              "fightName": "Vector Fight",
              "success": true,
              "players": [
                {
                  "name": "Player One",
                  "account": "Player.1234",
                  "profession": "Guardian",
                  "dpsAll": [ { "damage": 1000 } ],
                  "dpsTargets": [ [ { "damage": 900 } ] ],
                  "statsAll": [
                    {
                      "missed": 6,
                      "interrupts": 7,
                      "blocked": 10,
                      "killed": 12,
                      "downed": 13,
                      "downContribution": 17,
                      "distToCom": 123.45
                    }
                  ],
                  "statsTargets": [
                    [
                      {
                        "missed": 16,
                        "interrupts": 17,
                        "blocked": 20,
                        "killed": 22,
                        "downed": 23,
                        "downContribution": 27
                      }
                    ]
                  ],
                  "defenses": [
                    {
                      "damageTaken": 100,
                      "damageBarrier": 101,
                      "missedCount": 102,
                      "interruptedCount": 103,
                      "blockedCount": 106,
                      "boonStrips": 110,
                      "downCount": 112,
                      "deadCount": 114
                    }
                  ],
                  "support": [
                    {
                      "condiCleanseSelf": 1,
                      "condiCleanseTimeSelf": 1.5,
                      "condiCleanse": 2,
                      "condiCleanseTime": 2.5,
                      "boonStrips": 4
                    }
                  ],
                  "extHealingStats": {
                    "outgoingHealing": [ { "healing": 111 } ],
                    "incomingHealing": [ { "healing": 222 } ],
                    "outgoingHealingAllies": [ [ { "healing": 333 } ] ]
                  },
                  "extBarrierStats": {
                    "outgoingBarrier": [ { "barrier": 444 } ],
                    "incomingBarrier": [ { "barrier": 555 } ],
                    "outgoingBarrierAllies": [ [ { "barrier": 666 } ] ]
                  }
                }
              ],
              "targets": [ { "name": "Target" } ],
              "phases": [ { "name": "Phase 1", "start": 0, "end": 1000, "targets": [0] } ]
            }
            """,
            "https://dps.report/vector");

        var phase = Assert.Single(result.FightEliteInsightDataModel.Phases!);
        var offensiveStats = Assert.Single(phase.OffensiveStats!);
        Assert.Equal(6, offensiveStats[ArcDpsDataIndices.NumberOfHitsWhileBlindedIndex]);
        Assert.Equal(7, offensiveStats[ArcDpsDataIndices.InterruptsIndex]);
        Assert.Equal(10, offensiveStats[ArcDpsDataIndices.NumberOfTimesEnemyBlockedAttackIndex]);
        Assert.Equal(12, offensiveStats[ArcDpsDataIndices.EnemyDeathIndex]);
        Assert.Equal(13, offensiveStats[ArcDpsDataIndices.DownIndex]);
        Assert.Equal(17, offensiveStats[ArcDpsDataIndices.DamageDownContribution]);

        var offensiveTargetStats = Assert.Single(Assert.Single(phase.OffensiveStatsTargets!));
        Assert.Equal(17, offensiveTargetStats[ArcDpsDataIndices.InterruptsIndex]);
        Assert.Equal(22, offensiveTargetStats[ArcDpsDataIndices.EnemyDeathIndex]);

        var gameplayStats = Assert.Single(phase.GameplayStats!);
        Assert.Equal(123.45, gameplayStats[ArcDpsDataIndices.DistanceFromTagIndex]);

        var defStats = Assert.Single(phase.DefStats!);
        Assert.Equal(100, defStats[ArcDpsDataIndices.DamageTakenIndex].Double);
        Assert.Equal(101, defStats[ArcDpsDataIndices.BarrierMitigationIndex].Double);
        Assert.Equal(102, defStats[ArcDpsDataIndices.NumberOfMissesAgainstIndex].Double);
        Assert.Equal(103, defStats[ArcDpsDataIndices.TimesInterruptedIndex].Double);
        Assert.Equal(106, defStats[ArcDpsDataIndices.NumberOfTimesBlockedAttackIndex].Double);
        Assert.Equal(110, defStats[ArcDpsDataIndices.NumberOfBoonsRippedIndex].Double);
        Assert.Equal(112, defStats[ArcDpsDataIndices.EnemiesDownedIndex].Double);
        Assert.Equal(114, defStats[ArcDpsDataIndices.DeathIndex].Double);

        var supportStats = Assert.Single(phase.SupportStats!);
        Assert.Equal(2, supportStats[ArcDpsDataIndices.PlayerCleansesIndex]);
        Assert.Equal(4, supportStats[ArcDpsDataIndices.PlayerStripsIndex]);

        var healingPhase = Assert.Single(result.HealingEliteInsightDataModel.HealingPhases);
        Assert.Equal(111, Assert.Single(Assert.Single(healingPhase.OutgoingHealingStats)));
        Assert.Equal(333, Assert.Single(Assert.Single(Assert.Single(healingPhase.OutgoingHealingStatsTargets))));

        var barrierPhase = Assert.Single(result.BarrierEliteInsightDataModel.BarrierPhases);
        Assert.Equal(444, Assert.Single(Assert.Single(barrierPhase.OutgoingBarrierStats)));
        Assert.Equal(666, Assert.Single(Assert.Single(Assert.Single(barrierPhase.OutgoingBarrierStatsTargets))));

        Assert.NotNull(result.RawHealingData);
        Assert.Contains("healingPhases", result.RawHealingData);
        Assert.Contains("111", result.RawHealingData);
        Assert.NotNull(result.RawBarrierData);
        Assert.Contains("barrierPhases", result.RawBarrierData);
        Assert.Contains("444", result.RawBarrierData);
    }
}
