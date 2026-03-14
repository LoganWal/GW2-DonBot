namespace DonBot.Services.WordleServices;

public sealed class WordGeneratorService : IWordGeneratorService
{
    private readonly HashSet<string> _wordList = LoadWordList(Path.Combine("Resources", "wordleWords.txt"));

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

        // Words that differ from the target by exactly one character
        var similarWords = _wordList
            .Where(word =>
                word != wordleWord &&
                word.Length == wordleWord.Length &&
                word.Where((c, i) => c != wordleWord[i]).Count() == 1)
            .ToList();

        // Positions where the target word differs from its near-identical variants
        var distinctivePositions = new HashSet<int>();
        foreach (var similarWord in similarWords)
        {
            for (int i = 0; i < wordleWord.Length; i++)
            {
                if (wordleWord[i] != similarWord[i])
                {
                    distinctivePositions.Add(i);
                }
            }
        }

        var distinctiveLetters = distinctivePositions
            .Select(pos => wordleWord[pos])
            .ToHashSet();

        var random = new Random();
        var candidateWords = new List<string>();

        // Priority 1: words sharing distinctive letters with at least one green and one yellow match
        foreach (var word in _wordList)
        {
            if (word == wordleWord)
                continue;

            bool hasGreenLetter = false;
            bool hasYellowLetter = false;
            bool hasDistinctiveLetter = false;

            for (int i = 0; i < Math.Min(wordleWord.Length, word.Length); i++)
            {
                if (wordleWord[i] == word[i])
                {
                    hasGreenLetter = true;
                    break;
                }
            }

            foreach (var letter in wordleWord)
            {
                if (word.Contains(letter))
                {
                    if (!hasYellowLetter && word.IndexOf(letter) != wordleWord.IndexOf(letter))
                        hasYellowLetter = true;

                    if (!hasDistinctiveLetter && distinctiveLetters.Contains(letter))
                        hasDistinctiveLetter = true;
                }
            }

            if (distinctiveLetters.Count > 0)
            {
                if (hasGreenLetter && hasYellowLetter && hasDistinctiveLetter)
                    candidateWords.Add(word);
            }
            else if (hasGreenLetter && hasYellowLetter)
            {
                candidateWords.Add(word);
            }
        }

        // Priority 2: any word that shares a distinctive letter
        if (candidateWords.Count == 0 && distinctiveLetters.Count > 0)
        {
            candidateWords = _wordList
                .Where(word =>
                    word != wordleWord &&
                    distinctiveLetters.Any(word.Contains))
                .ToList();
        }

        // Priority 3: words with at least one green and one yellow letter match
        if (candidateWords.Count == 0)
        {
            candidateWords = _wordList
                .Where(word =>
                {
                    if (word == wordleWord)
                        return false;

                    bool hasGreenLetter = false;
                    bool hasYellowLetter = false;

                    for (int i = 0; i < Math.Min(word.Length, wordleWord.Length); i++)
                    {
                        if (wordleWord[i] == word[i])
                            hasGreenLetter = true;

                        for (int j = 0; j < wordleWord.Length; j++)
                        {
                            if (j != i && word.Contains(wordleWord[j]) &&
                                (word.IndexOf(wordleWord[j]) != j))
                            {
                                hasYellowLetter = true;
                            }
                        }
                    }

                    return hasGreenLetter && hasYellowLetter;
                })
                .ToList();
        }

        // Priority 4: any word sharing at least one letter with the target
        if (candidateWords.Count == 0)
        {
            candidateWords = _wordList
                .Where(word => word != wordleWord && word.Intersect(wordleWord).Any())
                .ToList();
        }

        if (candidateWords.Count == 0)
        {
            throw new InvalidOperationException("No suitable starting words found.");
        }

        return candidateWords[random.Next(candidateWords.Count)];
    }
}