using Microsoft.Extensions.Configuration;
using System.Security.Policy;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {

        public T? FetchBotAppSettings<T>(string key) where T: class
        {
            IConfiguration localConfig = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var cloudConfig = new ConfigurationBuilder()
                .AddAzureAppConfiguration(Environment.GetEnvironmentVariable("AzureConfigConnectionString"))
                .Build();

            var setting = localConfig[key] as T;
            if (setting == null || setting.ToString() == "")
            {
                setting = cloudConfig[key] as T;
            }

            return setting;
        }
    }
}
