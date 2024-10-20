namespace DonBot.Services.SecretsServices
{
    public interface ISecretService
    {
        public string FetchDonBotSqlConnectionString();

        public string FetchDonBotToken();
    }
}
