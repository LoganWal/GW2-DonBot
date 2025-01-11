using System.Text;
using DonBot.Models.Apis.WordleApi;
using Newtonsoft.Json;

namespace DonBot.Services.WordleServices
{
    public class DictionaryService(IHttpClientFactory httpClientFactory)
    {
        public async Task<string> GetDefinitionsAsync(string word)
        {
            string response;
            try
            {
                response = await httpClientFactory.CreateClient().GetStringAsync($"https://api.dictionaryapi.dev/api/v2/entries/en/{word}");
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
                    result.AppendLine($"- Definition: {(meaning.Definitions.FirstOrDefault()?.DefinitionText ?? "Unknown")}");
                }
            }

            return result.ToString();
        }
    }
}

