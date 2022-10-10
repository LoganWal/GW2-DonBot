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

        public async Task<BotSecretsDataModel> FetchBotAppSettings()
        {
            var appsettings = _cacheService.Get<BotSecretsDataModel>(CacheKey.Secrets);
            if (appsettings == null)
            {
                appsettings = await _fileService.ReadAndParse<BotSecretsDataModel>(FileLocation.AppSettings);

                if (appsettings == null)
                {
                    throw new Exception("Appsettings are null");
                }

                _cacheService.Set(CacheKey.Secrets, appsettings, DateTimeOffset.Now.AddMinutes(2));
            }

            return appsettings;
        }
    }
}
