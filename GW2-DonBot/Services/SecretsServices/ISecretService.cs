using Models;

namespace Services.SecretsServices
{
    public interface ISecretService
    {
        Task<BotSecretsDataModel> FetchBotAppSettings();
    }
}
