using System.Text.Json;
using System.Text.Json.Serialization;
using DonBot.Core.Models.Enums;

namespace DonBot.Core.Models.Scheduling;

public sealed record ScheduledEventResponseOption(
    string? Label,
    string? Emoji,
    bool Notify = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? AllowedRoleIds = null);

public static class ScheduledEventResponseOptions
{
    public const int MaxCount = 10;
    public const int MaxLabelLength = 80;
    public const int MaxEmojiLength = 64;
    private const int MaxJsonLength = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ScheduledEventResponseOption> ForEvent(
        short eventType,
        string? responseOptionsJson)
    {
        var stored = Deserialize(responseOptionsJson);
        if (stored.Count > 0)
        {
            return stored;
        }

        return DefaultsForEventType(eventType);
    }

    public static IReadOnlyList<ScheduledEventResponseOption> DefaultsForEventType(short eventType) =>
        eventType switch
        {
            (short)ScheduledEventTypeEnum.RaidSignup =>
            [
                new("Join", "✅"),
                new("Can't Join", "❌"),
                new("Can Fill", "🛠️")
            ],
            (short)ScheduledEventTypeEnum.WvwRaidSignup =>
            [
                new("Join", "✅"),
                new("Can't Join", "❌"),
                new("Will Be Late", "⏰")
            ],
            _ => []
        };

    public static IReadOnlyList<ScheduledEventResponseOption> Normalize(
        IEnumerable<ScheduledEventResponseOption?>? options)
    {
        if (options is null)
        {
            return [];
        }

        return options
            .Select(o => o is null
                ? new ScheduledEventResponseOption(string.Empty, string.Empty)
                : new ScheduledEventResponseOption(
                    (o.Label ?? string.Empty).Trim(),
                    (o.Emoji ?? string.Empty).Trim(),
                    o.Notify,
                    NormalizeRoleIds(o.AllowedRoleIds)))
            .ToList();
    }

    public static string Serialize(IEnumerable<ScheduledEventResponseOption?>? options) =>
        JsonSerializer.Serialize(Normalize(options), JsonOptions);

    public static string SerializeForEventType(short eventType, IEnumerable<ScheduledEventResponseOption?>? options)
    {
        if (!IsSignupEvent(eventType))
        {
            return string.Empty;
        }

        var normalized = Normalize(options);
        if (normalized.Count == 0)
        {
            normalized = DefaultsForEventType(eventType);
        }

        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static string? ValidateForEventType(
        short eventType,
        IReadOnlyList<ScheduledEventResponseOption>? options)
    {
        if (!IsSignupEvent(eventType))
        {
            return null;
        }

        var normalized = Normalize(options);
        if (normalized.Count == 0)
        {
            return null;
        }

        if (normalized.Count > MaxCount)
        {
            return $"Response options must include no more than {MaxCount} buttons.";
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in normalized)
        {
            if (string.IsNullOrWhiteSpace(option.Label))
            {
                return "Response option labels are required.";
            }

            if (option.Label.Length > MaxLabelLength)
            {
                return $"Response option labels must be {MaxLabelLength} characters or fewer.";
            }

            if (string.IsNullOrWhiteSpace(option.Emoji))
            {
                return "Response option emojis are required.";
            }

            if (option.Emoji.Length > MaxEmojiLength)
            {
                return $"Response option emojis must be {MaxEmojiLength} characters or fewer.";
            }

            if (!seen.Add($"{option.Emoji} {option.Label}"))
            {
                return "Response options must be unique.";
            }

            if (option.AllowedRoleIds?.Any(roleId => !ulong.TryParse(roleId, out var parsedRoleId) || parsedRoleId == 0) == true)
            {
                return "Allowed response roles must be valid Discord role ids.";
            }
        }

        if (Serialize(normalized).Length > MaxJsonLength)
        {
            return "Response options are too large.";
        }

        return null;
    }

    public static bool IsSignupEvent(short eventType) =>
        eventType == (short)ScheduledEventTypeEnum.RaidSignup
        || eventType == (short)ScheduledEventTypeEnum.WvwRaidSignup;

    public static short ToCurrentEventType(short eventType) =>
        eventType == (short)ScheduledEventTypeEnum.WvwRaidSignup
            ? (short)ScheduledEventTypeEnum.RaidSignup
            : eventType;

    public static string FieldName(ScheduledEventResponseOption option) =>
        $"{(option.Emoji ?? string.Empty).Trim()} {(option.Label ?? string.Empty).Trim()}".Trim();

    public static bool CanRespond(
        ScheduledEventResponseOption option,
        IEnumerable<ulong> userRoleIds)
    {
        if (option.AllowedRoleIds is null || option.AllowedRoleIds.Count == 0)
        {
            return true;
        }

        var allowedRoleIds = option.AllowedRoleIds
            .Select(roleId => ulong.TryParse(roleId, out var parsedRoleId) ? parsedRoleId : 0)
            .Where(roleId => roleId > 0)
            .ToHashSet();

        return allowedRoleIds.Count > 0 && userRoleIds.Any(allowedRoleIds.Contains);
    }

    private static IReadOnlyList<string>? NormalizeRoleIds(IEnumerable<string>? roleIds)
    {
        if (roleIds is null)
        {
            return null;
        }

        var normalized = roleIds
            .Select(roleId => roleId?.Trim() ?? string.Empty)
            .Where(roleId => roleId.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return normalized.Count == 0 ? null : normalized;
    }

    private static IReadOnlyList<ScheduledEventResponseOption> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var options = JsonSerializer.Deserialize<List<ScheduledEventResponseOption>>(json, JsonOptions);
            return Normalize(options);
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
