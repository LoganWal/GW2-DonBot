namespace DonBot.Services.WordleServices;

public sealed class WordGeneratorService : IWordGeneratorService
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

        // Find similar words that differ by just one character
        var similarWords = _wordList
            .Where(word =>
                word != wordleWord &&
                word.Length == wordleWord.Length &&
                word.Where((c, i) => c != wordleWord[i]).Count() == 1)
            .ToList();

        // Identify distinctive letters - these are the letters that make the wordle word 
        // different from its similar variants
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

        // Get the distinctive letters
        var distinctiveLetters = distinctivePositions
            .Select(pos => wordleWord[pos])
            .ToHashSet();

        var random = new Random();
        var candidateWords = new List<string>();

        // First priority: words with distinctive letters, plus green and yellow hints
        foreach (var word in _wordList)
        {
            // Skip the actual wordle word as starting word
            if (word == wordleWord)
                continue;

            bool hasGreenLetter = false;
            bool hasYellowLetter = false;
            bool hasDistinctiveLetter = false;

            // Check for green letters (same position)
            for (int i = 0; i < Math.Min(wordleWord.Length, word.Length); i++)
            {
                if (wordleWord[i] == word[i])
                {
                    hasGreenLetter = true;
                    break;
                }
            }

            // Check for yellow letters (different position) and distinctive letters
            foreach (var letter in wordleWord)
            {
                if (word.Contains(letter))
                {
                    // If it's at a different position, it's yellow
                    if (!hasYellowLetter && word.IndexOf(letter) != wordleWord.IndexOf(letter))
                    {
                        hasYellowLetter = true;
                    }

                    // If it's a distinctive letter, mark it
                    if (!hasDistinctiveLetter && distinctiveLetters.Contains(letter))
                    {
                        hasDistinctiveLetter = true;
                    }
                }
            }

            // If we found similar words, prioritize words with distinctive letters
            if (distinctiveLetters.Count > 0)
            {
                if (hasGreenLetter && hasYellowLetter && hasDistinctiveLetter)
                {
                    candidateWords.Add(word);
                }
            }
            // Otherwise use our standard criteria
            else if (hasGreenLetter && hasYellowLetter)
            {
                candidateWords.Add(word);
            }
        }

        // If we don't find words with all criteria, fall back to just including distinctive letters
        if (candidateWords.Count == 0 && distinctiveLetters.Count > 0)
        {
            candidateWords = _wordList
                .Where(word =>
                    word != wordleWord &&
                    distinctiveLetters.Any(letter => word.Contains(letter)))
                .ToList();
        }

        // If we still don't have candidates, fall back to words with green and yellow
        if (candidateWords.Count == 0)
        {
            candidateWords = _wordList
                .Where(word =>
                {
                    if (word == wordleWord)
                    {
                        return false;
                    }

                    bool hasGreenLetter = false;
                    bool hasYellowLetter = false;

                    for (int i = 0; i < Math.Min(word.Length, wordleWord.Length); i++)
                    {
                        // Green letter check
                        if (wordleWord[i] == word[i])
                        {
                            hasGreenLetter = true;
                        }

                        // Yellow letter check - word contains the letter but not at this position
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

        // Last resort fallback
        if (candidateWords.Count == 0)
        {
            candidateWords = _wordList
                .Where(word => word != wordleWord && word.Intersect(wordleWord).Count() >= 1)
                .ToList();
        }

        if (candidateWords.Count == 0)
        {
            throw new InvalidOperationException("No suitable starting words found.");
        }

        return candidateWords[random.Next(candidateWords.Count)];
    }
}