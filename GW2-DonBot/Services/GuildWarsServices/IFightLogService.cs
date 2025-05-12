using Discord.WebSocket;

namespace DonBot.Services.GuildWarsServices;

public interface IFightLogService
{
    public Task GetEnemyInformation(SocketMessageComponent eliteInsightDataModel);
}