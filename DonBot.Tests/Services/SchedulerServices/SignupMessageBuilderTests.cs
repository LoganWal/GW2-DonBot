using Discord;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.Scheduling;
using DonBot.Services.SchedulerServices;

namespace DonBot.Tests.Services.SchedulerServices;

public sealed class SignupMessageBuilderTests
{
    [Fact]
    public void BuildContent_PostedEventTimeExists_UsesPostedEventTime()
    {
        var scheduledEvent = new ScheduledEvent
        {
            UtcEventTime = new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc),
            PostedEventTime = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Message = "Monday Prog"
        };

        var content = SignupMessageBuilder.BuildContent(scheduledEvent);

        var expectedTimestamp = new DateTimeOffset(scheduledEvent.PostedEventTime.Value, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.StartsWith($"<t:{expectedTimestamp}:f>", content);
    }

    [Fact]
    public void BuildContent_PostedEventTimeMissing_UsesUtcEventTime()
    {
        var scheduledEvent = new ScheduledEvent
        {
            UtcEventTime = new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc),
            Message = "Monday Prog"
        };

        var content = SignupMessageBuilder.BuildContent(scheduledEvent);

        var expectedTimestamp = new DateTimeOffset(scheduledEvent.UtcEventTime, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.StartsWith($"<t:{expectedTimestamp}:f>", content);
    }

    [Fact]
    public void BuildEmbed_WithExistingLegacyFields_PreservesRosterValues()
    {
        var existingEmbed = new EmbedBuilder()
            .WithTitle("Event Roster")
            .AddField("✅ Roster", "<@1>\n\n**Total: 1**")
            .AddField("🛠️ Fillers", "<@2>\n\n**Total: 1**")
            .AddField("❌ Can't Join", "<@3>\n\n**Total: 1**")
            .Build();
        var scheduledEvent = new ScheduledEvent
        {
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            ResponseOptionsJson = ScheduledEventResponseOptions.Serialize([
                new ScheduledEventResponseOption("Join", "✅"),
                new ScheduledEventResponseOption("Can't Join", "❌"),
                new ScheduledEventResponseOption("Can Fill", "🛠️")
            ])
        };

        var updatedEmbed = SignupMessageBuilder.BuildEmbed(scheduledEvent, existingEmbed);

        Assert.Equal("<@1>\n\n**Total: 1**", updatedEmbed.Fields.Single(f => f.Name == "✅ Join").Value);
        Assert.Equal("<@2>\n\n**Total: 1**", updatedEmbed.Fields.Single(f => f.Name == "🛠️ Can Fill").Value);
        Assert.Equal("<@3>\n\n**Total: 1**", updatedEmbed.Fields.Single(f => f.Name == "❌ Can't Join").Value);
        Assert.Equal("**Total: 3**", updatedEmbed.Fields.Single(f => f.Name == SignupMessageBuilder.TotalResponsesFieldName).Value);
    }

    [Fact]
    public void BuildEmbed_WithoutResponses_AddsZeroOverallTotal()
    {
        var scheduledEvent = new ScheduledEvent
        {
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            ResponseOptionsJson = ScheduledEventResponseOptions.Serialize([
                new ScheduledEventResponseOption("Join", "✅"),
                new ScheduledEventResponseOption("Can't Join", "❌")
            ])
        };

        var embed = SignupMessageBuilder.BuildEmbed(scheduledEvent);

        Assert.Equal("No one has joined yet.\n\n**Total: 0**", embed.Fields.Single(f => f.Name == "✅ Join").Value);
        Assert.Equal("No one has declined yet.\n\n**Total: 0**", embed.Fields.Single(f => f.Name == "❌ Can't Join").Value);
        Assert.Equal("**Total: 0**", embed.Fields.Single(f => f.Name == SignupMessageBuilder.TotalResponsesFieldName).Value);
    }

    [Fact]
    public void BuildEmbed_WithExistingEmptyLegacyField_AddsZeroFieldTotal()
    {
        var existingEmbed = new EmbedBuilder()
            .WithTitle("Event Roster")
            .AddField("✅ Roster", "No one has joined yet.")
            .Build();
        var scheduledEvent = new ScheduledEvent
        {
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            ResponseOptionsJson = ScheduledEventResponseOptions.Serialize([
                new ScheduledEventResponseOption("Join", "✅")
            ])
        };

        var updatedEmbed = SignupMessageBuilder.BuildEmbed(scheduledEvent, existingEmbed);

        Assert.Equal("No one has joined yet.\n\n**Total: 0**", updatedEmbed.Fields.Single(f => f.Name == "✅ Join").Value);
        Assert.Equal("**Total: 0**", updatedEmbed.Fields.Single(f => f.Name == SignupMessageBuilder.TotalResponsesFieldName).Value);
    }

    [Fact]
    public void FieldKey_ForSameFieldName_IsStable()
    {
        var first = SignupMessageBuilder.FieldKey("✅ Join");
        var second = SignupMessageBuilder.FieldKey(new ScheduledEventResponseOption("Join", "✅"));

        Assert.Equal(first, second);
        Assert.Equal(12, first.Length);
    }
}
