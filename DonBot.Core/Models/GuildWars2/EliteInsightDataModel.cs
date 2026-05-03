namespace DonBot.Models.GuildWars2;

public class EliteInsightDataModel()
{
    public EliteInsightDataModel(string url) : this()
    {
        FightEliteInsightDataModel = new FightEliteInsightDataModel { Url = url };
    }

    public EliteInsightDataModel(FightEliteInsightDataModel fightData, HealingEliteInsightDataModel healingData, BarrierEliteInsightDataModel barrierData, string? rawFightData, string? rawHealingData, string? rawBarrierData) : this()
    {
        FightEliteInsightDataModel = fightData;
        HealingEliteInsightDataModel = healingData;
        BarrierEliteInsightDataModel = barrierData;
        RawFightData = rawFightData;
        RawHealingData = rawHealingData;
        RawBarrierData = rawBarrierData;
    }

    public FightEliteInsightDataModel FightEliteInsightDataModel { get; } = new();

    public HealingEliteInsightDataModel HealingEliteInsightDataModel { get; } = new();

    public BarrierEliteInsightDataModel BarrierEliteInsightDataModel { get; } = new();

    public string? RawFightData { get; }

    public string? RawHealingData { get; }

    public string? RawBarrierData { get; }
}