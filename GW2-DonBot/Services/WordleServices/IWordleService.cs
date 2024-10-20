namespace Services
{
    public interface IWordleService
    {
        Task<string> FetchWordleWord();
    }
}