using Models;

namespace GW2DonBot.Models
{
    static class CacheKey
    {
        public const string Secrets = nameof(BotSecretsDataModel);

        public const string SeenUrls = "SeenUrls";
    }
}
