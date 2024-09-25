namespace DonBotDayOff.Services;

public class WordGeneratorService : IWordGeneratorService
{
    private readonly HashSet<string> _wordList = LoadWordList("Wordle\\wordleWords.txt");

    private static HashSet<string> LoadWordList(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Word list file not found.", filePath);
        }

        var words = File.ReadAllLines(filePath)
            .Select(word => word.Trim())
            .Where(word => !string.IsNullOrEmpty(word))
            .ToHashSet();

        return words;
    }

    public string GenerateStartingWord(string wordleWord)
    {
        if (string.IsNullOrEmpty(wordleWord))
        {
            throw new ArgumentException("Invalid Wordle word.");
        }

        var random = new Random();
        var possibleWords = _wordList
            .Where(word => word.Intersect(wordleWord).Any())
            .ToList();

        if (possibleWords.Count == 0)
        {
            throw new InvalidOperationException("No suitable starting words found.");
        }

        return possibleWords[random.Next(possibleWords.Count)];
    }
}