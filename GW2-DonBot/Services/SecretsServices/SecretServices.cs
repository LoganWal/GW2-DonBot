using Microsoft.Extensions.Configuration;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {

        public T? FetchBotAppSettings<T>(string key) where T: class
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            return config[key] as T;
        }
    }
}
