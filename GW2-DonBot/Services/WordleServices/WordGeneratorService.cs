namespace DonBot.Services.WordleServices
{
    public class WordGeneratorService : IWordGeneratorService
    {
        private readonly HashSet<string> _wordList = LoadWordList(@"Resources\wordleWords.txt");

        private static HashSet<string> LoadWordList(string relativePath)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(basePath, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Word list file not found.", fullPath);
            }

            var words = File.ReadAllLines(fullPath)
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
                .Where(word => word.Intersect(wordleWord).Count() >= 1)
                .ToList();

            if (possibleWords.Count == 0)
            {
                throw new InvalidOperationException("No suitable starting words found.");
            }

            return possibleWords[random.Next(possibleWords.Count)];
        }
    }
}