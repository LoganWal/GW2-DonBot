using DonBot.Core.Services.GuildWars2;

namespace DonBot.Services.DiscordServices;

/// Extracts dps report URLs and rewrites Wingman log URLs for the parser.
public static class LogUrlExtractor
{
    public static List<string> ExtractFromText(string content) =>
        ReportUrlHelper.ExtractReportAndWingmanUrls(content);
}
