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
        var reparseAttempted = false;

        while (true)
        {
            try
            {
                using var response = await httpClientFactory.CreateClient().GetAsync(url);
                using var content = response.Content;
                var result = await content.ReadAsStringAsync();

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

                    var scriptContent = result.Substring(scriptStartIndex + "<script>".Length, scriptEndIndex - scriptStartIndex - "<script>".Length);
                    scriptTags.Add(scriptContent);

                    scriptStartIndex = scriptEndIndex + "</script>".Length;
                }

                var logDataScript = scriptTags.FirstOrDefault(script => script.Contains("_logData = {"));
                if (logDataScript == null)
                {
                    logger.LogError("Failed to locate _logData in any <script> tag.");
                    throw new InvalidOperationException("_logData JSON object not found in the HTML.");
                }

                var fightData = ExtractAndDeserialize<FightEliteInsightDataModel>(logDataScript, "_logData");
                var healingData = ExtractAndDeserialize<HealingEliteInsightDataModel>(logDataScript, "_healingStatsExtension");
                var barrierData = ExtractAndDeserialize<BarrierEliteInsightDataModel>(logDataScript, "_barrierStatsExtension");

                var rawFightData = ExtractRaw(logDataScript, "_logData");
                var rawHealingData = ExtractRaw(logDataScript, "_healingStatsExtension");
                var rawBarrierData = ExtractRaw(logDataScript, "_barrierStatsExtension");

                fightData.Url = url;

                return new EliteInsightDataModel(fightData, healingData, barrierData, rawFightData, rawHealingData, rawBarrierData);
            }
            catch (Exception ex)
            {
                attempt++;

                if (!reparseAttempted && IsWingmanUrl(url))
                {
                    reparseAttempted = true;
                    logger.LogInformation("Wingman log not yet parsed, triggering reparse for {url}", url);
                    try
                    {
                        await TriggerWingmanReparseAsync(url);
                        logger.LogInformation("Reparse completed for {url}, retrying fetch", url);
                        attempt = 0;
                        continue;
                    }
                    catch (Exception reparseEx)
                    {
                        logger.LogWarning(reparseEx, "Wingman reparse failed for {url}", url);
                    }
                }

                logger.LogWarning(ex, "Attempt {attempt} failed to retrieve or process data from URL: {url}. Retrying in 1 second...", attempt, url);

                if (attempt >= maxRetries)
                {
                    logger.LogError("Max retries reached. Returning an empty EliteInsightDataModel.");
                    return new EliteInsightDataModel(url);
                }

                await Task.Delay(1000);
            }
        }
    }

    private static bool IsWingmanUrl(string url) =>
        url.Contains("gw2wingman.nevermindcreations.de/logContent/");

    private async Task TriggerWingmanReparseAsync(string logContentUrl)
    {
        var reparseUrl = logContentUrl.Replace("/logContent/", "/reparse/");
        logger.LogInformation("Calling wingman reparse endpoint: {ReparseUrl}", reparseUrl);

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        using var response = await client.GetAsync(reparseUrl);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<WingmanReparseResponse>(json);

        if (result?.Result != true)
        {
            throw new InvalidOperationException($"Wingman reparse did not return success for {reparseUrl}. Response: {json}");
        }
    }

    private class WingmanReparseResponse
    {
        [JsonProperty("result")]
        public bool Result { get; set; }
    }

    private string? ExtractRaw(string script, string variableName)
    {
        try
        {
            var startMarker = $"{variableName} = {{";
            var rawStartIndex = script.IndexOf(startMarker, StringComparison.Ordinal);
            if (rawStartIndex < 0)
            {
                return null;
            }

            var startIndex = rawStartIndex + startMarker.Length - 1;
            var endIndex = script.IndexOf("};", startIndex, StringComparison.Ordinal) + 1;

            if (endIndex <= 0)
            {
                return null;
            }

            return script.Substring(startIndex, endIndex - startIndex);
        }
        catch
        {
            return null;
        }
    }

    private T ExtractAndDeserialize<T>(string script, string variableName) where T : new()
    {
        try
        {
            var json = ExtractRaw(script, variableName);
            if (json == null)
            {
                logger.LogInformation("{variableName} not present in log — stats extension likely not enabled.", variableName);
                return new T();
            }

            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Failed to deserialize {variableName} JSON — stats extension likely not enabled.", variableName);
            return new T();
        }
    }
}
