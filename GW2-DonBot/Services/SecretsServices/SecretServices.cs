using Microsoft.Extensions.Configuration;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {
        public T? Fetch<T>(string key) where T: class
        {
            IConfiguration localConfig = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var setting = localConfig[key] as T;
            if (setting == null || setting.ToString() == "")
            {
                try
                {
                    var cloudConfig = new ConfigurationBuilder().AddAzureAppConfiguration(Environment.GetEnvironmentVariable("AzureConfigConnectionString")).Build();
                    setting = cloudConfig[key] as T;
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed to fetch cloud configuration, either set a local app setting value for `{key}` or check you have an environment value for `AzureConfigConnectionString`");
                }
            }

            return setting;
        }

        public Dictionary<string, string> FetchAll()
        {
            var allSettings = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build().AsEnumerable().ToList();

            var dictSettings = new Dictionary<string, string>();
            foreach (var setting in allSettings)
            {
                if (setting.Value == null || setting.Value.ToString() == "")
                {
                    try
                    {
                        var cloudConfig = new ConfigurationBuilder().AddAzureAppConfiguration(Environment.GetEnvironmentVariable("AzureConfigConnectionString")).Build();
                        dictSettings.Add(setting.Key, cloudConfig[setting.Key]);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"Failed to fetch cloud configuration, either set a local app setting value for `{setting.Key}` or check you have an environment value for `AzureConfigConnectionString`");
                    }
                }
                else
                {
                    dictSettings.Add(setting.Key, setting.Value);
                }
            }

            return dictSettings;
        }
    }
}
