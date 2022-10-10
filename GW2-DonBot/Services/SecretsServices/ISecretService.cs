using Models;

namespace Services.SecretsServices
{
    public interface ISecretService
    {
        T? FetchBotAppSettings<T>(string key) where T : class;
    }
}
