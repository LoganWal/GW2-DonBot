using DonBot.Models.GuildWars2;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DonBot.Services.GuildWarsServices;

public class DataModelGenerationService(ILogger<DataModelGenerationService> logger, IHttpClientFactory httpClientFactory) : IDataModelGenerationService
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

                // Extract the _logData JSON object
                var logDataStartIndex = logDataScript.IndexOf("_logData = {", StringComparison.Ordinal) + "_logData = ".Length;
                var logDataEndIndex = logDataScript.IndexOf("};", logDataStartIndex, StringComparison.Ordinal) + 1;

                if (logDataStartIndex == -1 || logDataEndIndex == -1)
                {
                    logger.LogError("Failed to locate _logData JSON object in the script content.");
                    throw new InvalidOperationException("_logData JSON object not found.");
                }

                var data = logDataScript.Substring(logDataStartIndex, logDataEndIndex - logDataStartIndex);

                // Log the extracted JSON for debugging
                logger.LogDebug("Extracted _logData JSON: {data}", data);

                // Deserialize the JSON object
                var deserializeData = new EliteInsightDataModel();
                try
                {
                    deserializeData = JsonConvert.DeserializeObject<EliteInsightDataModel>(data) ?? new EliteInsightDataModel();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to deserialize _logData JSON.");
                }

                deserializeData.Url = url;
                return deserializeData;
            }
            catch (Exception ex)
            {
                attempt++;
                logger.LogWarning(ex, "Attempt {attempt} failed to retrieve or process data from URL: {url}. Retrying in 1 second...", attempt, url);

                if (attempt >= maxRetries)
                {
                    logger.LogError("Max retries reached. Returning an empty EliteInsightDataModel.");
                    return new EliteInsightDataModel { Url = url };
                }

                await Task.Delay(1000); // Wait for 1 second before retrying
            }
        }
    }
}