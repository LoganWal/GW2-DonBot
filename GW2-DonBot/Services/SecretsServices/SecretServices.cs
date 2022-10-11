using Microsoft.Extensions.Configuration;
using System.Security.Policy;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {

        public T? FetchBotAppSettings<T>(string key) where T: class
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            Console.WriteLine($"key is : {config[key] as T}");

            return config[key] as T;
        }
    }
}
