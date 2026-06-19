using System.Text.RegularExpressions;

namespace DonBot.Services.DiscordServices;

public sealed partial class ArtSpamDetector : IArtSpamDetector
{
    public bool IsSpam(string? content, bool hasImage)
    {
        if (!hasImage || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var text = Normalize(content);
        if (text.Length < 12)
        {
            return false;
        }

        if (TwitchEmoteBaitRegex().IsMatch(text))
        {
            return true;
        }

        var hasArtTerm = ArtTermRegex().IsMatch(text);
        if (!hasArtTerm)
        {
            return false;
        }

        if (StrongSolicitationRegex().IsMatch(text))
        {
            return true;
        }

        var solicitationMatches = SolicitationRegex().Matches(text).Count;
        var artMatches = ArtTermRegex().Matches(text).Count;

        return solicitationMatches >= 2 || artMatches >= 2 && solicitationMatches >= 1;
    }

    private static string Normalize(string content)
    {
        var lower = content.ToLowerInvariant();
        return WhitespaceRegex().Replace(lower, " ").Trim();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(?:twitch\s+emotes?|emotes?|vtuber|v\s*tuber|live\s*2d|l2d|2d\s+(?:vtuber\s+)?model|3d\s+(?:model(?:ing)?|vtuber\s+model)|animated\s+logo|logo|stream\s+package|model(?:ing)?)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ArtTermRegex();

    [GeneratedRegex(@"\b(?:commission(?:s)?|custom|interested|dms?|message\s+me|hmu|pricing|rates|limited\s+slots|slots|book\s+your\s+slots|discounted\s+price|send\s+me|slide\s+into|details|availability|client|offering)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SolicitationRegex();

    [GeneratedRegex(@"\b(?:commission(?:s)?\s+(?:open|done|completed)|commissions?\s+are\s+open|commission\s+open|dms?\s+are\s+open|send\s+me\s+a\s+dm|slide\s+into\s+my\s+dm|hmu|pricing|rates|limited\s+slots|book\s+your\s+slots)\b", RegexOptions.CultureInvariant)]
    private static partial Regex StrongSolicitationRegex();

    [GeneratedRegex(@"\btwitch\s+emotes?\b.*\b(?:worked\s+on\s+recently|let\s+me\s+know|opinions?|ideas?\s+for\s+improvement)\b|\b(?:worked\s+on\s+recently|let\s+me\s+know|opinions?|ideas?\s+for\s+improvement)\b.*\btwitch\s+emotes?\b", RegexOptions.CultureInvariant)]
    private static partial Regex TwitchEmoteBaitRegex();
}
