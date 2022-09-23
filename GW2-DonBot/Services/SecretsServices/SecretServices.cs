using Models;
using Newtonsoft.Json;
using Services.CacheServices;
using Services.FileServices;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {
        private readonly ICacheService cacheService;
        private readonly IFileService fileService;

        public SecretServices(ICacheService cacheService, IFileService fileService)
        {
            this.cacheService = cacheService;
            this.fileService = fileService;
        }

        public async Task<BotSecretsDataModel> FetchBotSecretsDataModel()
        {
            var secrets = cacheService.Get<BotSecretsDataModel>(nameof(BotSecretsDataModel));
            if (secrets == null)
            {
                secrets = await fileService.ReadAndParse<BotSecretsDataModel>("Secrets/botSecrets.json");

                if (secrets == null)
                {
                    throw new Exception("Secrets are null");
                }

                cacheService.Set(nameof(BotSecretsDataModel), secrets);
            }

            return secrets;
        }
    }
}
