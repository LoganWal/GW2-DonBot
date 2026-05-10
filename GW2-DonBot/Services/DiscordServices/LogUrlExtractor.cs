using System.Text.RegularExpressions;

namespace DonBot.Services.DiscordServices;

/// Extracts dps.report and gw2wingman log URLs from arbitrary text.
/// Wingman /log/ URLs are rewritten to /logContent/ which is what the parser pulls.
public static partial class LogUrlExtractor
{
    [GeneratedRegex(@"https://(?:b\.dps|wvw|dps)\.report/\S+")]
    private static partial Regex DpsReportRegex();

    [GeneratedRegex(@"https://gw2wingman\.nevermindcreations\.de/log/\S+")]
    private static partial Regex WingmanRegex();

    private const string WingmanLogPath = "https://gw2wingman.nevermindcreations.de/log/";
    private const string WingmanLogContentPath = "https://gw2wingman.nevermindcreations.de/logContent/";

    public static List<string> ExtractFromText(string content)
    {
        if (string.IsNullOrEmpty(content)) {
            return [];
        }

        var urls = DpsReportRegex().Matches(content).Select(m => m.Value).ToList();

        foreach (Match match in WingmanRegex().Matches(content))
        {
            urls.Add(match.Value.Replace(WingmanLogPath, WingmanLogContentPath));
        }

        return urls;
    }
}
