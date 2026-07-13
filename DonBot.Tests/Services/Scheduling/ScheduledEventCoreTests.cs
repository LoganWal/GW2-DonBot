using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;
using DonBot.Core.Models.Scheduling;
using DonBot.Core.Services.Scheduling;

namespace DonBot.Tests.Services.Scheduling;

public sealed class ScheduledEventCoreTests
{
    private static readonly DateTime Now = new(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

    private static ScheduledEventWriteRequest ValidRequest(
        short eventType = (short)ScheduledEventTypeEnum.RaidSignup,
        long channelId = 12345,
        DateTime? utcEventTime = null,
        IReadOnlyList<ScheduledEventResponseOption>? responseOptions = null) =>
        new(
            eventType,
            channelId,
            5,
            19,
            utcEventTime ?? Now.AddDays(1),
            7,
            "  Reset CM  ",
            responseOptions,
            30);

    [Fact]
    public void ValidateWriteRequest_HappyPath_ReturnsNull()
    {
        Assert.Null(ScheduledEventRules.ValidateWriteRequest(ValidRequest(), Now));
    }

    [Fact]
    public void ValidateWriteRequest_RejectsLegacyWvwSignup()
    {
        var error = ScheduledEventRules.ValidateWriteRequest(
            ValidRequest(eventType: (short)ScheduledEventTypeEnum.WvwRaidSignup),
            Now);

        Assert.Contains("consolidated", error);
    }

    [Fact]
    public void ValidateWriteRequest_RejectsPastEventTime()
    {
        var error = ScheduledEventRules.ValidateWriteRequest(
            ValidRequest(utcEventTime: Now.AddMinutes(-1)),
            Now);

        Assert.Contains("future", error);
    }

    [Fact]
    public void GetFormMetadata_ExposesSchedulingLimitsAndAllowedTypes()
    {
        var metadata = ScheduledEventRules.GetFormMetadata();

        Assert.Equal(256, metadata.MaxMessageLength);
        Assert.Equal(15, metadata.DefaultNotificationMinutesBeforeStart);
        Assert.Contains(metadata.EventTypes, e => e.EventType == (short)ScheduledEventTypeEnum.RaidSignup);
        Assert.DoesNotContain(metadata.EventTypes, e => e.EventType == (short)ScheduledEventTypeEnum.WvwRaidSignup);
        Assert.NotEmpty(metadata.DefaultSignupResponseOptions);
    }

    [Fact]
    public void BuildEvent_NormalizesValuesAndSerializesDefaultResponseOptions()
    {
        var entity = ScheduledEventPlanner.BuildEvent(999, ValidRequest(responseOptions: null));

        Assert.Equal(999, entity.GuildId);
        Assert.Equal(12345, entity.ChannelId);
        Assert.Equal("Reset CM", entity.Message);
        Assert.Equal(DateTimeKind.Utc, entity.UtcEventTime.Kind);
        Assert.Equal(["Join", "Can't Join", "Can Fill"],
            ScheduledEventResponseOptions.ForEvent(entity.EventType, entity.ResponseOptionsJson).Select(o => o.Label));
    }

    [Fact]
    public void ApplyForUpdate_PreservesExistingSignupOptionsWhenRequestOmitsOptions()
    {
        var existing = new ScheduledEvent
        {
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            ResponseOptionsJson = ScheduledEventResponseOptions.Serialize([
                new ScheduledEventResponseOption("Scout", "👀", true)
            ])
        };

        ScheduledEventPlanner.ApplyForUpdate(existing, ValidRequest(responseOptions: null));

        var option = Assert.Single(ScheduledEventResponseOptions.ForEvent(existing.EventType, existing.ResponseOptionsJson));
        Assert.Equal("Scout", option.Label);
        Assert.True(option.Notify);
    }

    [Fact]
    public void ResponseOptions_SerializeAndNormalizeAllowedRoles()
    {
        var json = ScheduledEventResponseOptions.Serialize([
            new ScheduledEventResponseOption("Join", "✅", AllowedRoleIds: [" 100 ", "200", "100", ""])
        ]);

        var option = Assert.Single(ScheduledEventResponseOptions.ForEvent(
            (short)ScheduledEventTypeEnum.RaidSignup,
            json));

        Assert.Equal(["100", "200"], option.AllowedRoleIds);
    }

    [Fact]
    public void ResponseOptions_CanRespond_AllowsAnyoneWhenNoRolesConfigured()
    {
        var option = new ScheduledEventResponseOption("Join", "✅");

        Assert.True(ScheduledEventResponseOptions.CanRespond(option, []));
    }

    [Fact]
    public void ResponseOptions_CanRespond_RequiresAtLeastOneConfiguredRole()
    {
        var option = new ScheduledEventResponseOption("Join", "✅", AllowedRoleIds: ["100", "200"]);

        Assert.True(ScheduledEventResponseOptions.CanRespond(option, [50UL, 200UL]));
        Assert.False(ScheduledEventResponseOptions.CanRespond(option, [50UL, 60UL]));
    }

    [Fact]
    public void ValidateWriteRequest_RejectsInvalidAllowedRoleId()
    {
        var error = ScheduledEventRules.ValidateWriteRequest(
            ValidRequest(responseOptions:
            [
                new ScheduledEventResponseOption("Join", "✅", AllowedRoleIds: ["not-a-role"])
            ]),
            Now);

        Assert.Contains("role ids", error);
    }

    [Fact]
    public void Snapshot_CanDetectChangesAndRestoreMutableState()
    {
        var entity = ScheduledEventPlanner.BuildEvent(999, ValidRequest());
        entity.MessageId = 555;
        entity.PostedEventTime = Now.AddHours(1);
        var snapshot = ScheduledEventSnapshot.Capture(entity);

        entity.Message = "changed";
        Assert.False(snapshot.Matches(entity));

        snapshot.ApplyTo(entity);

        Assert.True(snapshot.Matches(entity));
        Assert.True(snapshot.IsPostedSignup);
    }

    [Fact]
    public void AdvanceAfterFire_UsesPersistedRepeatInterval()
    {
        var fired = new ScheduledEvent
        {
            Day = 5,
            UtcEventTime = Now
        };
        var persisted = new ScheduledEvent
        {
            Day = 5,
            RepeatIntervalDays = 4,
            UtcEventTime = Now
        };

        ScheduledEventRecurrence.AdvanceAfterFire(persisted, fired);

        Assert.Equal(Now.AddDays(4), persisted.UtcEventTime);
        Assert.Equal(2, persisted.Day);
    }
}
