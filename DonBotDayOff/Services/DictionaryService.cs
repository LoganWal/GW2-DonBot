using System.Text;
using DonBotDayOff.Models;
using Newtonsoft.Json;

namespace DonBotDayOff.Services;

public class DictionaryService(HttpClient httpClient)
{
    public async Task<string> GetDefinitionsAsync(string word)
    {
        var url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{word}";
        var response = await httpClient.GetStringAsync(url);

        var dictionaryResponse = JsonConvert.DeserializeObject<DictionaryApiResponse[]>(response);

        if (dictionaryResponse == null || dictionaryResponse.Length == 0)
        {
            return "No definitions found.";
        }

        var result = new StringBuilder();

        foreach (var entry in dictionaryResponse)
        {
            foreach (var meaning in entry.Meanings)
            {
                result.AppendLine($"Part of Speech: {meaning.PartOfSpeech}");
                foreach (var definition in meaning.Definitions)
                {
                    result.AppendLine($"- Definition: {definition.DefinitionText}");
                }
            }
        }

        return result.ToString();
    }
}