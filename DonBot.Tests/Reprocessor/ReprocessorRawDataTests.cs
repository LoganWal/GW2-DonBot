using DonBot.Reprocessor;

namespace DonBot.Tests.Reprocessor;

public class ReprocessorRawDataTests
{
    [Fact]
    public void DeserializeFightData_IncludesStoredHealingAndBarrierRawData()
    {
        const string rawFightData = """
        {
          "url": "https://dps.report/raw",
          "logName": "Stored Raw Fight",
          "phases": [
            {
              "name": "Phase 1",
              "encounterDuration": "00:01:00.000"
            }
          ],
          "players": []
        }
        """;
        const string rawHealingData = """
        {
          "healingPhases": [
            {
              "outgoingHealingStats": [[123]],
              "outgoingHealingStatsTargets": [[[456]]],
              "incomingHealingStats": [[789]]
            }
          ]
        }
        """;
        const string rawBarrierData = """
        {
          "barrierPhases": [
            {
              "outgoingBarrierStats": [[321]],
              "outgoingBarrierStatsTargets": [[[654]]],
              "incomingBarrierStats": [[987]]
            }
          ]
        }
        """;

        var row = new FightRawBatchRow(
            42,
            1,
            60_000,
            rawFightData,
            rawHealingData,
            rawBarrierData);

        var result = ReprocessorRawData.DeserializeFightData(row);

        Assert.Equal("https://dps.report/raw", result.FightEliteInsightDataModel.Url);
        Assert.Equal("Stored Raw Fight", result.FightEliteInsightDataModel.LogName);
        Assert.Equal(123, result.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStats[0][0]);
        Assert.Equal(456, result.HealingEliteInsightDataModel.HealingPhases[0].OutgoingHealingStatsTargets[0][0][0]);
        Assert.Equal(321, result.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStats[0][0]);
        Assert.Equal(654, result.BarrierEliteInsightDataModel.BarrierPhases[0].OutgoingBarrierStatsTargets[0][0][0]);
        Assert.Same(rawFightData, result.RawFightData);
        Assert.Same(rawHealingData, result.RawHealingData);
        Assert.Same(rawBarrierData, result.RawBarrierData);
    }
}
