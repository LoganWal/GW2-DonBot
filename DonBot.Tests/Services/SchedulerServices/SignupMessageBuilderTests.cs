using Discord;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.Scheduling;
using DonBot.Services.SchedulerServices;

namespace DonBot.Tests.Services.SchedulerServices;

public sealed class SignupMessageBuilderTests
{
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
