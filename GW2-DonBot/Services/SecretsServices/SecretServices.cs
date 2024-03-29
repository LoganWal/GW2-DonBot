﻿namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {
        public string FetchDonBotSqlConnectionString()
        {
            var donBotSqlConnectionString = Environment.GetEnvironmentVariable("DonBotSqlConnectionString", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(donBotSqlConnectionString))
            {
                throw new Exception("DonBotSqlConnectionString does not exist");
            }

            return donBotSqlConnectionString;
        }

        public string FetchDonBotToken()
        {
            var donBotToken = Environment.GetEnvironmentVariable("DonBotToken", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(donBotToken))
            {
                throw new Exception("DonBotToken does not exist");
            }

            return donBotToken;
        }
    }
}
