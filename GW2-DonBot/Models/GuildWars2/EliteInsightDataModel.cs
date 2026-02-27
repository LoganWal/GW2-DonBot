namespace DonBot.Models.GuildWars2;

public class EliteInsightDataModel()
{
    public EliteInsightDataModel(string url) : this()
    {
        FightEliteInsightDataModel = new FightEliteInsightDataModel { Url = url };
    }

    public EliteInsightDataModel(FightEliteInsightDataModel fightData, HealingEliteInsightDataModel healingData, BarrierEliteInsightDataModel barrierData) : this()
    {
        FightEliteInsightDataModel = fightData;
        HealingEliteInsightDataModel = healingData;
        BarrierEliteInsightDataModel = barrierData;
    }

    public FightEliteInsightDataModel FightEliteInsightDataModel { get; set; } = new FightEliteInsightDataModel();

    public HealingEliteInsightDataModel HealingEliteInsightDataModel { get; set; } = new HealingEliteInsightDataModel();

    public BarrierEliteInsightDataModel BarrierEliteInsightDataModel { get; set; } = new BarrierEliteInsightDataModel();
}