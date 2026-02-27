namespace DonBot.Services.WordleServices;

public interface IWordleService
{
    Task<string> FetchWordleWord();
}