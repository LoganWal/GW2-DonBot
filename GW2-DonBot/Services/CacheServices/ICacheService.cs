namespace Services.CacheServices
{
    public interface ICacheService
    {
        void Set<T>(string key, T value) where T : notnull;

        T? Get<T>(string key) where T : class;
    }
}
