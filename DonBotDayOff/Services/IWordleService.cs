namespace DonBotDayOff.Services;

public interface IWordleService
{
    Task<string> FetchWordleWord();
}