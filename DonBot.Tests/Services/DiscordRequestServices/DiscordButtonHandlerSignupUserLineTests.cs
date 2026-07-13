using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.Scheduling;
using DonBot.Services.DiscordRequestServices;
using DonBot.Services.SchedulerServices;

namespace DonBot.Tests.Services.DiscordRequestServices;

public sealed class DiscordButtonHandlerSignupUserLineTests
{
    [Fact]
    public void CanUseResponseOption_UnrestrictedOption_AllowsMemberWithoutRoles()
    {
        var option = new ScheduledEventResponseOption("Join", "✅");

        Assert.True(DiscordButtonHandler.CanUseResponseOption([], option));
    }

    [Fact]
    public void CanUseResponseOption_RestrictedOption_RequiresMatchingRole()
    {
        var option = new ScheduledEventResponseOption("Join", "✅", AllowedRoleIds: ["100", "200"]);

        Assert.True(DiscordButtonHandler.CanUseResponseOption([50UL, 200UL], option));
        Assert.False(DiscordButtonHandler.CanUseResponseOption([50UL, 60UL], option));
    }

    [Fact]
    public void ResolveResponseOption_ModernFieldKeyWinsOverStaleIndex()
    {
        var scheduledEvent = BuildScheduledEvent();
        var expected = ScheduledEventResponseOptions
            .ForEvent(scheduledEvent.EventType, scheduledEvent.ResponseOptionsJson)[2];

        var option = DiscordButtonHandler.ResolveResponseOption(
            scheduledEvent,
            optionIndex: 0,
            fieldKey: SignupMessageBuilder.FieldKey(expected));

        Assert.Equal("Can Fill", option?.Label);
        Assert.Equal(["300"], option?.AllowedRoleIds);
    }

    [Fact]
    public void ResolveResponseOption_LegacyIndexFindsRestrictedOption()
    {
        var option = DiscordButtonHandler.ResolveResponseOption(
            BuildScheduledEvent(),
            optionIndex: 2,
            fieldKey: null);

        Assert.Equal("Can Fill", option?.Label);
        Assert.False(DiscordButtonHandler.CanUseResponseOption([100UL], option!));
        Assert.True(DiscordButtonHandler.CanUseResponseOption([300UL], option!));
    }

    [Fact]
    public void FormatUserLine_WithUsername_AppendsAccountName()
    {
        var line = DiscordButtonHandler.FormatUserLine("<@123>", "TestUser");

        Assert.Equal("<@123> (TestUser)", line);
    }

    [Theory]
    [InlineData("<@123>")]
    [InlineData("<@123> (TestUser)")]
    [InlineData("<@123>(TestUser)")]
    [InlineData("<@!123>")]
    [InlineData("<@!123> (TestUser)")]
    [InlineData("TestUser")]
    public void IsSameUserLine_ForCurrentAndLegacyFormats_MatchesUser(string line)
    {
        var matches = DiscordButtonHandler.IsSameUserLine(line, 123, "TestUser");

        Assert.True(matches);
    }

    [Theory]
    [InlineData("<@1234> (TestUser)")]
    [InlineData("<@12> (TestUser)")]
    [InlineData("OtherUser")]
    public void IsSameUserLine_ForDifferentUser_DoesNotMatch(string line)
    {
        var matches = DiscordButtonHandler.IsSameUserLine(line, 123, "TestUser");

        Assert.False(matches);
    }

    private static ScheduledEvent BuildScheduledEvent() =>
        new()
        {
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            ResponseOptionsJson = ScheduledEventResponseOptions.Serialize([
                new ScheduledEventResponseOption("Join", "✅"),
                new ScheduledEventResponseOption("Can't Join", "❌"),
                new ScheduledEventResponseOption("Can Fill", "🛠️", AllowedRoleIds: ["300"])
            ])
        };
}
