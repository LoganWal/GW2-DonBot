using Models;

namespace Services.PlayerServices
{
    public interface IPlayerService
    {
        public Task SetPlayerPoints(EliteInsightDataModel eliteInsightDataModel);

        public List<Gw2Player> GetGw2Players(EliteInsightDataModel data, ArcDpsPhase fightPhase, HealingPhase healingPhase, BarrierPhase barrierPhase, short? encounterType = null, bool sumAllTargets = true);
    }
}
