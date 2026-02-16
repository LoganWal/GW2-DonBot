using DonBot.Models.GuildWars2;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.GuildWarsServices;

public sealed class DataModelGenerationService(ILogger<DataModelGenerationService> logger, IHttpClientFactory httpClientFactory) : IDataModelGenerationService
{
    public async Task<EliteInsightDataModel> GenerateEliteInsightDataModelFromUrl(string url)
    {
        const int maxRetries = 3;
        var attempt = 0;

        while (true)
        {
            try
            {
                using var response = await httpClientFactory.CreateClient().GetAsync(url);
                using var content = response.Content;
                var result = await content.ReadAsStringAsync();

                // Extract all <script> tags from the HTML
                var scriptTags = new List<string>();
                var scriptStartIndex = 0;

                while ((scriptStartIndex = result.IndexOf("<script>", scriptStartIndex, StringComparison.Ordinal)) != -1)
                {
                    var scriptEndIndex = result.IndexOf("</script>", scriptStartIndex, StringComparison.Ordinal);
                    if (scriptEndIndex == -1)
                    {
                        logger.LogError("Malformed HTML: Missing closing </script> tag.");
                        throw new InvalidOperationException("Malformed HTML: Missing closing </script> tag.");
                    }

                    // Extract the content of the script tag
                    var scriptContent = result.Substring(scriptStartIndex + "<script>".Length, scriptEndIndex - scriptStartIndex - "<script>".Length);
                    scriptTags.Add(scriptContent);

                    scriptStartIndex = scriptEndIndex + "</script>".Length;
                }

                // Find the script containing _logData
                var logDataScript = scriptTags.FirstOrDefault(script => script.Contains("_logData = {"));
                if (logDataScript == null)
                {
                    logger.LogError("Failed to locate _logData in any <script> tag.");
                    throw new InvalidOperationException("_logData JSON object not found in the HTML.");
                }

                // Extract and deserialize the data models
                var fightData = ExtractAndDeserialize<FightEliteInsightDataModel>(logDataScript, "_logData");
                var healingData = ExtractAndDeserialize<HealingEliteInsightDataModel>(logDataScript, "_healingStatsExtension");
                var barrierData = ExtractAndDeserialize<BarrierEliteInsightDataModel>(logDataScript, "_barrierStatsExtension");

                fightData.Url = url;

                return new EliteInsightDataModel(fightData, healingData, barrierData);
            }
            catch (Exception ex)
            {
                attempt++;
                logger.LogWarning(ex, "Attempt {attempt} failed to retrieve or process data from URL: {url}. Retrying in 1 second...", attempt, url);

                if (attempt >= maxRetries)
                {
                    logger.LogError("Max retries reached. Returning an empty EliteInsightDataModel.");
                    return new EliteInsightDataModel(url);
                }

                await Task.Delay(1000); // Wait for 1 second before retrying
            }
        }
    }

    private T ExtractAndDeserialize<T>(string script, string variableName) where T : new()
    {
        try
        {
            // Extract the JSON object
            var startMarker = $"{variableName} = {{";
            var startIndex = script.IndexOf(startMarker, StringComparison.Ordinal) + startMarker.Length - 1;
            var endIndex = script.IndexOf("};", startIndex, StringComparison.Ordinal) + 1;

            if (startIndex < 0 || endIndex <= 0)
            {
                logger.LogWarning("Failed to locate {variableName} in script content.", variableName);
                return new T();
            }

            var jsonData = script.Substring(startIndex, endIndex - startIndex);

            // Deserialize the JSON object
            return JsonConvert.DeserializeObject<T>(jsonData) ?? new T();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize {variableName} JSON.", variableName);
            return new T();
        }
    }
}