using Discord.WebSocket;

namespace DonBot.Services.LogServices
{
    public interface IFightLogService
    {
        public Task GetEnemyInformation(SocketMessageComponent eliteInsightDataModel);
    }
}
