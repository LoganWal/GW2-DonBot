namespace DonBot.Services.CacheServices
{
    public interface ICacheService
    {
        void Set<T>(string key, T value, DateTimeOffset? expiry = null) where T : notnull;

        T? Get<T>(string key) where T : class;
    }
}
