namespace DonBot.Services.SecretsServices;

public sealed class SecretServices : ISecretService
{
    public string FetchDonBotSqlConnectionString()
    {
        var donBotSqlConnectionString = Environment.GetEnvironmentVariable("DonBotSqlConnectionString");

        if (string.IsNullOrEmpty(donBotSqlConnectionString))
        {
            throw new Exception("DonBotSqlConnectionString does not exist");
        }

        return donBotSqlConnectionString;
    }

    public string FetchDonBotToken()
    {
        var donBotToken = Environment.GetEnvironmentVariable("DonBotToken");

        if (string.IsNullOrEmpty(donBotToken))
        {
            throw new Exception("DonBotToken does not exist");
        }

        return donBotToken;
    }

    public string FetchDiscordClientId()
    {
        var clientId = Environment.GetEnvironmentVariable("DiscordClientId");

        if (string.IsNullOrEmpty(clientId))
        {
            throw new Exception("DiscordClientId does not exist");
        }

        return clientId;
    }

    public string FetchDiscordClientSecret()
    {
        var clientSecret = Environment.GetEnvironmentVariable("DiscordClientSecret");

        if (string.IsNullOrEmpty(clientSecret))
        {
            throw new Exception("DiscordClientSecret does not exist");
        }

        return clientSecret;
    }
}