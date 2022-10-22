using Models;

namespace Services.SecretsServices
{
    public interface ISecretService
    {
        T? Fetch<T>(string key) where T : class;

        Dictionary<string, string> FetchAll();
    }
}
