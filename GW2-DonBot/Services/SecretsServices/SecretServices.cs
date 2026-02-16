﻿namespace DonBot.Services.SecretsServices;

public sealed class SecretServices : ISecretService
{
    public string FetchDonBotSqlConnectionString()
    {
        var donBotSqlConnectionString = Environment.GetEnvironmentVariable("DonBotSqlConnectionString", EnvironmentVariableTarget.Machine);

        if (string.IsNullOrEmpty(donBotSqlConnectionString))
        {
            throw new Exception("DonBotSqlConnectionString does not exist");
        }

        return donBotSqlConnectionString;
    }

    public string FetchDonBotToken()
    {
        var donBotToken = Environment.GetEnvironmentVariable("DonBotToken", EnvironmentVariableTarget.Machine);

        if (string.IsNullOrEmpty(donBotToken))
        {
            throw new Exception("DonBotToken does not exist");
        }

        return donBotToken;
    }
}