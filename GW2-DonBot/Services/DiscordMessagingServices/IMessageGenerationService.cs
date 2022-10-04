using Discord;
using Models;

namespace Services.DiscordMessagingServices
{
    public interface IMessageGenerationService
    {
        public Embed GenerateFightSummary(BotSecretsDataModel secrets, EliteInsightDataModel data);

        public Embed GenerateBadBehaviourPing(BotSecretsDataModel secrets);
    }
}
