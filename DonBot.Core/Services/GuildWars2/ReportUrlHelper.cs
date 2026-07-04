using System.Text.RegularExpressions;

namespace DonBot.Core.Services.GuildWars2;

public enum ReportUrlKind
{
    DpsReport,
    WvwReport
}

public sealed record ParsedReportUrl(
    string OriginalUrl,
    string CanonicalUrl,
    string GetJsonUrl,
    string Permalink,
    string Host,
    ReportUrlKind Kind)
{
    public bool IsWvw => Kind == ReportUrlKind.WvwReport;
}

public static partial class ReportUrlHelper
{
    private const string DpsReportBaseUrl = "https://dps.report";
    private const string WvwReportBaseUrl = "https://wvw.report";
    private const string WingmanLogPath = "https://gw2wingman.nevermindcreations.de/log/";
    private const string WingmanLogContentPath = "https://gw2wingman.nevermindcreations.de/logContent/";

    private static readonly char[] UrlTrimChars = ['.', ',', ')'];

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlCandidateRegex();

    [GeneratedRegex(@"https://gw2wingman\.nevermindcreations\.de/log/\S+", RegexOptions.IgnoreCase)]
    private static partial Regex WingmanLogRegex();

    public static bool TryParseReportUrl(
        string? url,
        out ParsedReportUrl parsed,
        bool requireHttps = false)
    {
        parsed = null!;
        var trimmedUrl = TrimUrlCandidate(url);
        if (string.IsNullOrWhiteSpace(trimmedUrl) ||
            !Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (requireHttps && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        var kind = host switch
        {
            "dps.report" or "b.dps.report" => ReportUrlKind.DpsReport,
            "wvw.report" => ReportUrlKind.WvwReport,
            _ => (ReportUrlKind?)null
        };

        if (kind == null)
        {
            return false;
        }

        var baseUrl = kind == ReportUrlKind.WvwReport ? WvwReportBaseUrl : DpsReportBaseUrl;
        var path = uri.AbsolutePath.Trim('/');
        var isGetJsonEndpoint = uri.AbsolutePath.Equals("/getJson", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path) && !isGetJsonEndpoint)
        {
            return false;
        }

        var requestUrl = trimmedUrl;
        var permalink = path;
        var canonicalUrl = string.IsNullOrWhiteSpace(permalink)
            ? trimmedUrl
            : $"{baseUrl}/{permalink}";

        if (isGetJsonEndpoint)
        {
            permalink = GetQueryParameter(uri.Query, "permalink") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(permalink))
            {
                return false;
            }

            canonicalUrl = $"{baseUrl}/{permalink}";
        }
        else if (!string.IsNullOrWhiteSpace(permalink))
        {
            requestUrl = $"{baseUrl}/getJson?permalink={Uri.EscapeDataString(permalink)}";
        }

        parsed = new ParsedReportUrl(
            trimmedUrl,
            canonicalUrl,
            requestUrl,
            permalink,
            host,
            kind.Value);
        return true;
    }

    public static bool IsReportUrl(string? url, bool requireHttps = false) =>
        TryParseReportUrl(url, out _, requireHttps);

    public static string CanonicalizeReportUrl(string url, bool requireHttps = false) =>
        TryParseReportUrl(url, out var parsed, requireHttps)
            ? parsed.CanonicalUrl
            : url;

    public static string? ExtractFirstReportUrl(string? text, bool requireHttps = true) =>
        ExtractReportUrls(text, requireHttps).FirstOrDefault();

    public static List<string> ExtractReportUrls(string? text, bool requireHttps = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return UrlCandidateRegex()
            .Matches(text)
            .Select(match => TrimUrlCandidate(match.Value))
            .Where(candidate => IsReportUrl(candidate, requireHttps))
            .ToList();
    }

    public static List<string> ExtractReportAndWingmanUrls(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var urls = ExtractReportUrls(text, requireHttps: true);
        foreach (Match match in WingmanLogRegex().Matches(text))
        {
            urls.Add(ConvertWingmanLogToLogContentUrl(match.Value));
        }

        return urls;
    }

    public static string GetLogSource(string url, string fallback = "unknown") =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : fallback;

    private static string ConvertWingmanLogToLogContentUrl(string url) =>
        TrimUrlCandidate(url).Replace(WingmanLogPath, WingmanLogContentPath, StringComparison.OrdinalIgnoreCase);

    private static string TrimUrlCandidate(string? url) =>
        (url ?? string.Empty).Trim().TrimEnd(UrlTrimChars);

    private static string? GetQueryParameter(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? part[..separatorIndex] : part;
            if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return separatorIndex >= 0 ? Uri.UnescapeDataString(part[(separatorIndex + 1)..]) : string.Empty;
        }

        return null;
    }
}
