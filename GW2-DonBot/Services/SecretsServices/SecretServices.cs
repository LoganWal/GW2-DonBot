using Microsoft.Extensions.Configuration;

namespace DonBot.Services.SecretsServices;

public sealed class SecretServices(IConfiguration configuration) : ISecretService
{
    private string GetRequired(string key)
    {
        var value = configuration[key] ?? Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) {
            throw new InvalidOperationException($"'{key}' is not configured. Set it in appsettings.user.json or the .env file.");
        }
        return value;
    }

    public string FetchDonBotSqlConnectionString() => GetRequired("DonBotSqlConnectionString");
    public string FetchDonBotToken() => GetRequired("DonBotToken");
    public string FetchDiscordClientId() => GetRequired("DiscordClientId");
    public string FetchDiscordClientSecret() => GetRequired("DiscordClientSecret");
}
