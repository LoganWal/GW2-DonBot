using Microsoft.Extensions.Configuration;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {

        public T? FetchBotAppSettings<T>(string key) where T: class
        {
            Console.WriteLine("Quick little test");
            IConfiguration localConfig = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var setting = localConfig[key] as T;
            if (setting == null || setting.ToString() == "")
            {
                try
                {
                    var cloudConfig = new ConfigurationBuilder()
                        .AddAzureAppConfiguration(Environment.GetEnvironmentVariable("AzureConfigConnectionString"))
                        .Build();

                    setting = cloudConfig[key] as T;
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to fetch cloud configuration, either set a local app setting value for `{key}` or check you have an environment value for `AzureConfigConnectionString`");
                }
            }

            return setting;
        }
    }
}
