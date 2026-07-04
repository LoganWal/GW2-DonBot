using DonBot.Core.Models.GuildWars2;
using Newtonsoft.Json;

namespace DonBot.Core.Services.GuildWars2;

public static class EliteInsightRawDataSerializer
{
    public static EliteInsightDataModel Deserialize(
        string rawFightData,
        string? rawHealingData = null,
        string? rawBarrierData = null)
    {
        var fightData = DeserializeFight(rawFightData);
        var healingData = DeserializeHealing(rawHealingData);
        var barrierData = DeserializeBarrier(rawBarrierData);

        return new EliteInsightDataModel(
            fightData,
            healingData,
            barrierData,
            rawFightData,
            rawHealingData,
            rawBarrierData);
    }

    public static FightEliteInsightDataModel DeserializeFight(string? rawFightData) =>
        string.IsNullOrWhiteSpace(rawFightData)
            ? new FightEliteInsightDataModel()
            : JsonConvert.DeserializeObject<FightEliteInsightDataModel>(rawFightData) ?? new FightEliteInsightDataModel();

    public static HealingEliteInsightDataModel DeserializeHealing(string? rawHealingData) =>
        string.IsNullOrWhiteSpace(rawHealingData)
            ? new HealingEliteInsightDataModel()
            : JsonConvert.DeserializeObject<HealingEliteInsightDataModel>(rawHealingData) ?? new HealingEliteInsightDataModel();

    public static BarrierEliteInsightDataModel DeserializeBarrier(string? rawBarrierData) =>
        string.IsNullOrWhiteSpace(rawBarrierData)
            ? new BarrierEliteInsightDataModel()
            : JsonConvert.DeserializeObject<BarrierEliteInsightDataModel>(rawBarrierData) ?? new BarrierEliteInsightDataModel();
}
