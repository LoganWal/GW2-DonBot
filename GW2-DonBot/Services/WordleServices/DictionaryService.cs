using System.Text;
using DonBot.Models;
using Newtonsoft.Json;

namespace DonBot.Services.WordleServices
{
    public class DictionaryService
    {
        private readonly HttpClient _httpClient;

        public DictionaryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetDefinitionsAsync(string word)
        {
            var url = $"https://api.dictionaryapi.dev/api/v2/entries/en/{word}";
            string response;
            try
            {
                response = await _httpClient.GetStringAsync(url);
            }
            catch (Exception)
            {
                response = string.Empty;
            }

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
}

