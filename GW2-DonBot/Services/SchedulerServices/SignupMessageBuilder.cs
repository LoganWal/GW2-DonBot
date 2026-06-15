using System.Security.Cryptography;
using System.Text;
using Discord;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Scheduling;
using DonBot.Models.Statics;

namespace DonBot.Services.SchedulerServices;

internal static class SignupMessageBuilder
{
    public const string TotalResponsesFieldName = "Total Responses";

    public static string BuildContent(ScheduledEvent scheduledEvent)
    {
        var messageEventTime = scheduledEvent.PostedEventTime ?? scheduledEvent.UtcEventTime;
        var utcEventTime = DateTime.SpecifyKind(messageEventTime, DateTimeKind.Utc);
        var unixTimestamp = new DateTimeOffset(utcEventTime, TimeSpan.Zero).ToUnixTimeSeconds();
        return $"<t:{unixTimestamp}:f>\n{scheduledEvent.Message}";
    }

    public static Embed BuildEmbed(ScheduledEvent scheduledEvent, Embed? existingEmbed = null)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Event Roster")
            .WithDescription(string.Empty)
            .WithColor(Color.Blue);

        List<EmbedFieldBuilder> existingFields = existingEmbed?.ToEmbedBuilder().Fields.ToList() ?? [];
        var consumedExistingFieldIndexes = new HashSet<int>();

        foreach (var option in GetOptions(scheduledEvent))
        {
            var fieldName = ScheduledEventResponseOptions.FieldName(option);
            var preservedValue = FindPreservedFieldValue(fieldName, existingFields, consumedExistingFieldIndexes);
            var value = preservedValue is null
                ? GetDefaultFieldValue(fieldName)
                : NormalizeFieldValue(fieldName, preservedValue);
            embed.AddField(fieldName, value);
        }

        UpdateTotalResponsesField(embed);
        return embed.Build();
    }

    public static MessageComponent BuildComponents(ScheduledEvent scheduledEvent)
    {
        var builder = new ComponentBuilder();
        var options = GetOptions(scheduledEvent);

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            builder.WithButton(
                option.Label,
                customId: $"{ButtonId.ScheduledEventResponsePrefix}{scheduledEvent.ScheduledEventId}_{i}_{FieldKey(option)}",
                style: GetButtonStyle(i),
                emote: ParseEmoji(option.Emoji ?? string.Empty),
                row: i / 5);
        }

        return builder.Build();
    }

    public static string FieldKey(ScheduledEventResponseOption option) =>
        FieldKey(ScheduledEventResponseOptions.FieldName(option));

    public static string FieldKey(string fieldName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(fieldName));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    public static string GetDefaultFieldValue(string fieldName) =>
        FormatResponseFieldValue(fieldName, []);

    public static string FormatResponseFieldValue(string fieldName, IReadOnlyCollection<string> users)
    {
        if (users.Count == 0)
        {
            return $"{GetDefaultResponseText(fieldName)}\n\n**Total: 0**";
        }

        return string.Join('\n', users) + $"\n\n**Total: {users.Count}**";
    }

    private static string GetDefaultResponseText(string fieldName) => fieldName switch
    {
        "✅ Join" => "No one has joined yet.",
        "✅ Roster" => "No one has joined yet.",
        "❌ Can't Join" => "No one has declined yet.",
        "🛠️ Can Fill" => "No fillers yet.",
        "🛠️ Fillers" => "No fillers yet.",
        "⏰ Will Be Late" => "No one will be late.",
        _ => "No responses yet."
    };

    public static bool IsTotalResponsesField(string fieldName) =>
        string.Equals(fieldName, TotalResponsesFieldName, StringComparison.Ordinal);

    public static List<string> GetResponseUserList(EmbedFieldBuilder field)
    {
        if (IsTotalResponsesField(field.Name))
        {
            return [];
        }

        var fieldValue = field.Value as string ?? string.Empty;
        return fieldValue
            .Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line) &&
                           !line.StartsWith("**Total:") &&
                           !GetDefaultResponseText(field.Name).Equals(line))
            .ToList();
    }

    public static void UpdateTotalResponsesField(EmbedBuilder embed)
    {
        for (var i = embed.Fields.Count - 1; i >= 0; i--)
        {
            if (IsTotalResponsesField(embed.Fields[i].Name))
            {
                embed.Fields.RemoveAt(i);
            }
        }

        var total = embed.Fields.Sum(field => GetResponseUserList(field).Count);
        embed.AddField(TotalResponsesFieldName, $"**Total: {total}**");
    }

    private static string NormalizeFieldValue(string fieldName, string fieldValue)
    {
        var field = new EmbedFieldBuilder()
            .WithName(fieldName)
            .WithValue(fieldValue);
        return FormatResponseFieldValue(fieldName, GetResponseUserList(field));
    }

    private static string? FindPreservedFieldValue(
        string fieldName,
        IReadOnlyList<EmbedFieldBuilder> existingFields,
        HashSet<int> consumedExistingFieldIndexes)
    {
        foreach (var candidateName in GetPreservationFieldNames(fieldName))
        {
            for (var i = 0; i < existingFields.Count; i++)
            {
                if (consumedExistingFieldIndexes.Contains(i)
                    || existingFields[i].Name != candidateName)
                {
                    continue;
                }

                consumedExistingFieldIndexes.Add(i);
                return existingFields[i].Value?.ToString();
            }
        }

        return null;
    }

    private static IEnumerable<string> GetPreservationFieldNames(string fieldName)
    {
        yield return fieldName;

        switch (fieldName)
        {
            case "✅ Join":
                yield return "✅ Roster";
                break;
            case "✅ Roster":
                yield return "✅ Join";
                break;
            case "🛠️ Can Fill":
                yield return "🛠️ Fillers";
                break;
            case "🛠️ Fillers":
                yield return "🛠️ Can Fill";
                break;
        }
    }

    private static ButtonStyle GetButtonStyle(int index) => index switch
    {
        0 => ButtonStyle.Success,
        1 => ButtonStyle.Secondary,
        _ => ButtonStyle.Primary
    };

    private static IEmote? ParseEmoji(string emoji)
    {
        try
        {
            return Emoji.Parse(emoji);
        }
        catch (Exception)
        {
            // Try custom Discord emotes below.
        }

        try
        {
            return Emote.Parse(emoji);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReadOnlyList<ScheduledEventResponseOption> GetOptions(ScheduledEvent scheduledEvent) =>
        ScheduledEventResponseOptions.ForEvent(scheduledEvent.EventType, scheduledEvent.ResponseOptionsJson);
}
