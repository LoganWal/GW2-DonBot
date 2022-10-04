using GW2DonBot.Models;
using GW2DonBot.Models.Statics;
using Models;
using Services.CacheServices;
using Services.FileServices;

namespace Services.SecretsServices
{
    public class SecretServices : ISecretService
    {
        private readonly ICacheService _cacheService;
        private readonly IFileService _fileService;

        public SecretServices(ICacheService cacheService, IFileService fileService)
        {
            _cacheService = cacheService;
            _fileService = fileService;
        }

        public async Task<BotSecretsDataModel> FetchBotSecretsDataModel()
        {
            var secrets = _cacheService.Get<BotSecretsDataModel>(CacheKey.Secrets);
            if (secrets == null)
            {
                secrets = await _fileService.ReadAndParse<BotSecretsDataModel>(FileLocation.Secrets);

                if (secrets == null)
                {
                    throw new Exception("Secrets are null");
                }

                _cacheService.Set(CacheKey.Secrets, secrets);
            }

            return secrets;
        }
    }
}
