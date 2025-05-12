namespace DonBot.Services.WordleServices;

public interface IWordGeneratorService
{
    string GenerateStartingWord(string wordleWord);
}