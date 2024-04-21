using Discord.WebSocket;

namespace Services.PlayerServices
{
    public interface IFightLogService
    {
        public Task GetEnemyInformation(SocketMessageComponent eliteInsightDataModel);
    }
}
