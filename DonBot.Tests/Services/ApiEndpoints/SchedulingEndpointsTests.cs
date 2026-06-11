using DonBot.Api.Endpoints;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Models.Scheduling;

namespace DonBot.Tests.Services.ApiEndpoints;

public class SchedulingEndpointsTests
{
    private static readonly HashSet<ulong> Channels = new() { 100UL, 200UL };

    private static SchedulingEndpoints.EventWriteDto ValidBody(
        short eventType = (short)ScheduledEventTypeEnum.RaidSignup,
        string channelId = "100",
        DateTime? utcEventTime = null,
        short day = 1,
        short hour = 19,
        short repeat = 7,
        string? message = null) =>
        new(
            eventType,
            channelId,
            day,
            hour,
            utcEventTime ?? DateTime.UtcNow.AddDays(1),
            repeat,
            message);

    [Fact]
    public void ValidateEvent_HappyPath_ReturnsNull()
    {
        Assert.Null(SchedulingEndpoints.ValidateEvent(ValidBody(), Channels));
    }

    [Fact]
    public void ValidateEvent_UndefinedEventType_ReturnsError()
    {
        var body = ValidBody(eventType: 99);
        Assert.Contains("event type", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_RemovedWordleType_Rejected()
    {
        // EventType 3 used to be Wordle (removed). Must stay rejected so old rows
        // can't be re-created via the API.
        var body = ValidBody(eventType: 3);
        Assert.NotNull(SchedulingEndpoints.ValidateEvent(body, Channels));
    }

    [Fact]
    public void ValidateEvent_LegacyWvwSignupType_Rejected()
    {
        var body = ValidBody(eventType: (short)ScheduledEventTypeEnum.WvwRaidSignup);
        var error = SchedulingEndpoints.ValidateEvent(body, Channels);

        Assert.NotNull(error);
        Assert.Contains("consolidated", error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void ValidateEvent_InvalidDay_ReturnsError(short day)
    {
        var body = ValidBody(day: day);
        Assert.Contains("Post day", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void ValidateEvent_InvalidHour_ReturnsError(short hour)
    {
        var body = ValidBody(hour: hour);
        Assert.Contains("Post hour", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_DefaultUtcEventTime_ReturnsError()
    {
        var body = ValidBody(utcEventTime: default(DateTime));
        Assert.Contains("Event time", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_PastUtcEventTime_ReturnsError()
    {
        var body = ValidBody(utcEventTime: DateTime.UtcNow.AddMinutes(-5));
        Assert.Contains("future", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void ValidateEvent_RepeatOutOfRange_ReturnsError(short repeat)
    {
        var body = ValidBody(repeat: repeat);
        Assert.Contains("Repeat interval", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_ChannelNotInGuild_ReturnsError()
    {
        var body = ValidBody(channelId: "999");
        Assert.Contains("Channel", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_NonNumericChannelId_ReturnsError()
    {
        var body = ValidBody(channelId: "abc");
        Assert.NotNull(SchedulingEndpoints.ValidateEvent(body, Channels));
    }

    [Fact]
    public void ValidateEvent_MessageAtLimit_ReturnsNull()
    {
        var body = ValidBody(message: new string('a', SchedulingEndpoints.MaxMessageLength));
        Assert.Null(SchedulingEndpoints.ValidateEvent(body, Channels));
    }

    [Fact]
    public void ValidateEvent_MessageOverLimit_ReturnsError()
    {
        var body = ValidBody(message: new string('a', SchedulingEndpoints.MaxMessageLength + 1));
        var error = SchedulingEndpoints.ValidateEvent(body, Channels);
        Assert.NotNull(error);
        Assert.Contains("256", error);
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionsAtLimit_ReturnsNull()
    {
        var options = Enumerable.Range(1, ScheduledEventResponseOptions.MaxCount)
            .Select(i => new ScheduledEventResponseOption($"Option {i}", "✅"))
            .ToList();
        var body = ValidBody() with { ResponseOptions = options };

        Assert.Null(SchedulingEndpoints.ValidateEvent(body, Channels));
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionsOverLimit_ReturnsError()
    {
        var options = Enumerable.Range(1, ScheduledEventResponseOptions.MaxCount + 1)
            .Select(i => new ScheduledEventResponseOption($"Option {i}", "✅"))
            .ToList();
        var body = ValidBody() with { ResponseOptions = options };

        Assert.Contains("Response options", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionMissingLabel_ReturnsError()
    {
        var body = ValidBody() with
        {
            ResponseOptions = [new ScheduledEventResponseOption(" ", "✅")]
        };

        Assert.Contains("labels", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionMissingEmoji_ReturnsError()
    {
        var body = ValidBody() with
        {
            ResponseOptions = [new ScheduledEventResponseOption("Join", " ")]
        };

        Assert.Contains("emojis", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionNullStrings_ReturnsError()
    {
        var body = ValidBody() with
        {
            ResponseOptions = [new ScheduledEventResponseOption(null!, null!)]
        };

        Assert.Contains("labels", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionDuplicate_ReturnsError()
    {
        var body = ValidBody() with
        {
            ResponseOptions =
            [
                new ScheduledEventResponseOption("Join", "✅"),
                new ScheduledEventResponseOption("join", "✅")
            ]
        };

        Assert.Contains("unique", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionInvalidEmoji_ReturnsError()
    {
        var body = ValidBody() with
        {
            ResponseOptions = [new ScheduledEventResponseOption("Join", "not-an-emoji")]
        };

        Assert.Contains("valid Unicode", SchedulingEndpoints.ValidateEvent(body, Channels) ?? "");
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionCustomEmoji_ReturnsNull()
    {
        var body = ValidBody() with
        {
            ResponseOptions = [new ScheduledEventResponseOption("Join", "<:donbot:123456789012345678>")]
        };

        Assert.Null(SchedulingEndpoints.ValidateEvent(body, Channels, [123456789012345678UL]));
    }

    [Fact]
    public void ValidateEvent_SignupResponseOptionUnknownCustomEmoji_ReturnsError()
    {
        var body = ValidBody() with
        {
            ResponseOptions = [new ScheduledEventResponseOption("Join", "<:donbot:123456789012345678>")]
        };

        Assert.Contains("server custom", SchedulingEndpoints.ValidateEvent(body, Channels, []) ?? "");
    }

    [Fact]
    public void ValidateEvent_LeaderboardIgnoresResponseOptions_ReturnsNull()
    {
        var options = Enumerable.Range(1, ScheduledEventResponseOptions.MaxCount + 1)
            .Select(i => new ScheduledEventResponseOption($"Option {i}", "✅"))
            .ToList();
        var body = ValidBody(eventType: (short)ScheduledEventTypeEnum.PveLeaderboard) with { ResponseOptions = options };

        Assert.Null(SchedulingEndpoints.ValidateEvent(body, Channels));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseRoleIds_NullOrWhitespace_ReturnsEmpty(string? input)
    {
        Assert.Empty(SchedulingEndpoints.ParseRoleIds(input));
    }

    [Fact]
    public void ParseRoleIds_CsvWithJunk_FiltersAndDedupes()
    {
        var ids = SchedulingEndpoints.ParseRoleIds("100, 200 , abc, 100, , 300");
        Assert.Equal(new HashSet<ulong> { 100UL, 200UL, 300UL }, ids);
    }

    [Fact]
    public void ToDto_StringifiesIdsAndCopiesFields()
    {
        var fire = new DateTime(2030, 1, 1, 19, 0, 0, DateTimeKind.Utc);
        var entity = new ScheduledEvent
        {
            ScheduledEventId = 7,
            GuildId = 1,
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            ChannelId = 12345,
            Day = 1,
            Hour = 19,
            RepeatIntervalDays = 14,
            Message = "be there",
            ResponseOptionsJson = ScheduledEventResponseOptions.Serialize([
                new ScheduledEventResponseOption("Scout", "👀")
            ]),
            UtcEventTime = fire,
        };

        var dto = SchedulingEndpoints.ToDto(entity);

        Assert.Equal(7, dto.ScheduledEventId);
        Assert.Equal("12345", dto.ChannelId);
        Assert.Equal(14, dto.RepeatIntervalDays);
        Assert.Equal(fire, dto.UtcEventTime);
        Assert.Equal("be there", dto.Message);
        var option = Assert.Single(dto.ResponseOptions);
        Assert.Equal("Scout", option.Label);
        Assert.Equal("👀", option.Emoji);
    }

    [Fact]
    public void ToDto_MissingSignupResponseOptions_ReturnsEventDefaults()
    {
        var fire = new DateTime(2030, 1, 1, 19, 0, 0, DateTimeKind.Utc);
        var entity = new ScheduledEvent
        {
            ScheduledEventId = 8,
            GuildId = 1,
            EventType = (short)ScheduledEventTypeEnum.WvwRaidSignup,
            ChannelId = 12345,
            Day = 1,
            Hour = 19,
            RepeatIntervalDays = 14,
            Message = "be there",
            UtcEventTime = fire,
        };

        var dto = SchedulingEndpoints.ToDto(entity);

        Assert.Equal((short)ScheduledEventTypeEnum.RaidSignup, dto.EventType);
        Assert.Equal(new[] { "Join", "Can't Join", "Will Be Late" }, dto.ResponseOptions.Select(o => o.Label));
    }
}
