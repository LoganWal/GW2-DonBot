using DonBot.Models.GuildWars2;

namespace DonBot.Services.GuildWarsServices;

public interface IPlayerService
{
    public Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel);

    public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, short? encounterType = null, bool someAllFights = true);
}