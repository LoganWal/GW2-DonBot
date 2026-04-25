using System.Linq.Expressions;
using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;
using DonBot.Services.DatabaseServices;
using DonBot.Services.SchedulerServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace DonBot.Tests.Services.SchedulerServices;

public class SchedulerServiceTests
{
    // Friday 2026-03-20 10:00:00 UTC — used as a stable "now" across tests
    private static readonly DateTime Now = new(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc);

    private static ScheduledEvent MakeWeeklyEvent(
        short day,
        short hour,
        DateTime? utcEventTime = null,
        long id = 1,
        short repeatIntervalDays = 7) =>
        new()
        {
            ScheduledEventId = id,
            GuildId = 111,
            ChannelId = 222,
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            Day = day,
            Hour = hour,
            RepeatIntervalDays = repeatIntervalDays,
            UtcEventTime = utcEventTime ?? Now.AddDays(7)
        };

    private static SchedulerService BuildService(FakeEntityService? entityService = null) =>
        new(
            entityService ?? new FakeEntityService(),
            NullLogger<SchedulerService>.Instance,
            new DiscordSocketClient(),
            []);

    // -------------------------------------------------------------------------
    // GetNextEventTime — RepeatIntervalDays guard
    // -------------------------------------------------------------------------

    [Fact]
    public void GetNextEventTime_RepeatIntervalDaysZero_ReturnsMaxValue()
    {
        var ev = MakeWeeklyEvent(day: 5, hour: 4, repeatIntervalDays: 0);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DateTime.MaxValue, result);
    }

    [Fact]
    public void GetNextEventTime_RepeatIntervalDaysNegative_ReturnsMaxValue()
    {
        var ev = MakeWeeklyEvent(day: 5, hour: 4, repeatIntervalDays: -1);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DateTime.MaxValue, result);
    }

    // -------------------------------------------------------------------------
    // GetNextEventTime — fire day is in the future this week
    // -------------------------------------------------------------------------

    [Fact]
    public void GetNextEventTime_FireDayIsTomorrow_ReturnsTomorrow()
    {
        // now = Friday; fire on Saturday (6) at 4am
        var ev = MakeWeeklyEvent(day: 6, hour: 4);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DayOfWeek.Saturday, result.DayOfWeek);
        Assert.Equal(4, result.Hour);
        Assert.Equal(0, result.Minute);
        Assert.Equal(0, result.Second);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.True(result > Now);
    }

    [Fact]
    public void GetNextEventTime_FireDayIsInThreeDays_ReturnsCorrectDate()
    {
        // now = Friday (5); fire on Monday (1)
        var ev = MakeWeeklyEvent(day: 1, hour: 8);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.Equal(8, result.Hour);
        Assert.True(result > Now);
    }

    // -------------------------------------------------------------------------
    // GetNextEventTime — fire day is today
    // -------------------------------------------------------------------------

    [Fact]
    public void GetNextEventTime_FireDayIsToday_HourNotYetReached_ReturnsTodayAtHour()
    {
        // now = Friday 10:00; fire on Friday (5) at 14:00
        var ev = MakeWeeklyEvent(day: 5, hour: 14);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(Now.Date.AddHours(14), result);
        Assert.Equal(DayOfWeek.Friday, result.DayOfWeek);
    }

    [Fact]
    public void GetNextEventTime_FireDayIsToday_HourAlreadyPassed_ReturnsNextWeek()
    {
        // now = Friday 10:00; fire on Friday (5) at 04:00 — already passed today
        var ev = MakeWeeklyEvent(day: 5, hour: 4);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DayOfWeek.Friday, result.DayOfWeek);
        Assert.Equal(4, result.Hour);
        Assert.True(result > Now.AddDays(6)); // must be next Friday, not today
    }

    [Fact]
    public void GetNextEventTime_FireDayIsToday_ExactlyAtFireTime_ReturnsNextWeek()
    {
        // now = Friday 10:00; fire on Friday (5) at 10:00 exactly — not strictly greater, so next week
        var ev = MakeWeeklyEvent(day: 5, hour: 10);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DayOfWeek.Friday, result.DayOfWeek);
        Assert.True(result > Now);
        Assert.True(result > Now.AddDays(6));
    }

    // -------------------------------------------------------------------------
    // GetNextEventTime — fire day was earlier this week (next occurrence is next week)
    // -------------------------------------------------------------------------

    [Fact]
    public void GetNextEventTime_FireDayWasEarlierThisWeek_ReturnsNextWeek()
    {
        // now = Friday (5); fire on Tuesday (2) — Tuesday already passed this week
        var ev = MakeWeeklyEvent(day: 2, hour: 4);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DayOfWeek.Tuesday, result.DayOfWeek);
        Assert.Equal(4, result.Hour);
        Assert.True(result > Now);
    }

    // -------------------------------------------------------------------------
    // GetNextEventTime — boundary hours
    // -------------------------------------------------------------------------

    [Fact]
    public void GetNextEventTime_MidnightHour_ReturnsMidnightOnFireDay()
    {
        // fire on Saturday at midnight UTC
        var ev = MakeWeeklyEvent(day: 6, hour: 0);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(0, result.Hour);
        Assert.Equal(DayOfWeek.Saturday, result.DayOfWeek);
    }

    [Fact]
    public void GetNextEventTime_Hour23_Returns23OnFireDay()
    {
        var ev = MakeWeeklyEvent(day: 6, hour: 23);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(23, result.Hour);
        Assert.Equal(DayOfWeek.Saturday, result.DayOfWeek);
    }

    // -------------------------------------------------------------------------
    // GetNextEventTime — Sunday (DayOfWeek = 0 boundary)
    // -------------------------------------------------------------------------

    [Fact]
    public void GetNextEventTime_FireDaySunday_ReturnsNextSunday()
    {
        // now = Friday (5); fire on Sunday (0)
        var ev = MakeWeeklyEvent(day: 0, hour: 12);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.Equal(DayOfWeek.Sunday, result.DayOfWeek);
        Assert.Equal(12, result.Hour);
        Assert.True(result > Now);
    }

    // -------------------------------------------------------------------------
    // GetNextEventTime — result is always strictly in the future
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]  // Sunday
    [InlineData(1)]  // Monday
    [InlineData(2)]  // Tuesday
    [InlineData(3)]  // Wednesday
    [InlineData(4)]  // Thursday
    [InlineData(5)]  // Friday
    [InlineData(6)]  // Saturday
    public void GetNextEventTime_AllDaysOfWeek_ResultIsAlwaysStrictlyInFuture(int day)
    {
        var ev = MakeWeeklyEvent(day: (short)day, hour: 10);

        var result = SchedulerService.GetNextEventTime(ev, Now);

        Assert.True(result > Now, $"Day={day}: expected result to be strictly after now but got {result}");
    }

    // -------------------------------------------------------------------------
    // FastForwardEventIfBehind — no-op cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FastForwardEventIfBehind_UtcEventTimeInFuture_NoChangeNoDbWrite()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now.AddDays(3));
        var originalDay = ev.Day;
        var originalTime = ev.UtcEventTime;

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(originalDay, ev.Day);
        Assert.Equal(originalTime, ev.UtcEventTime);
        Assert.Equal(0, entityService.FakeScheduledEvent.UpdateCallCount);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_RepeatIntervalDaysZero_NoChangeNoDbWrite()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now.AddDays(-1), repeatIntervalDays: 0);

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(0, entityService.FakeScheduledEvent.UpdateCallCount);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_RepeatIntervalDaysNegative_NoChangeNoDbWrite()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now.AddDays(-1), repeatIntervalDays: -5);

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(0, entityService.FakeScheduledEvent.UpdateCallCount);
    }

    // -------------------------------------------------------------------------
    // FastForwardEventIfBehind — advances correctly
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FastForwardEventIfBehind_OneWeekBehind_AdvancesOneInterval()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var originalTime = Now.AddDays(-7);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: originalTime);

        await service.FastForwardEventIfBehind(ev, Now);

        // Now - 7 + 7 = Now, still <= Now, so advances again to Now + 7
        Assert.Equal(Now.AddDays(7), ev.UtcEventTime);
        Assert.True(ev.UtcEventTime > Now);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_TwoWeeksBehind_AdvancesTwoIntervals()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var originalTime = Now.AddDays(-14);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: originalTime);

        await service.FastForwardEventIfBehind(ev, Now);

        // Now - 14 → Now - 7 → Now (still <= Now) → Now + 7
        Assert.Equal(Now.AddDays(7), ev.UtcEventTime);
        Assert.True(ev.UtcEventTime > Now);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_UtcEventTimeExactlyNow_AdvancesOneInterval()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now);

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(Now.AddDays(7), ev.UtcEventTime);
        Assert.True(ev.UtcEventTime > Now);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_WeeklyEvent_DayUnchangedAfterAdvance()
    {
        // For interval=7: (Day + 7) % 7 = Day, so Day should not change
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now.AddDays(-7));
        var originalDay = ev.Day;

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(originalDay, ev.Day);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_Interval4_DayAdvancesCorrectly()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        // Day=5, interval=4: Now-4 → Now (still <=Now) → Now+4; two advances → Day=(5+4+4)%7=6
        var ev = new ScheduledEvent
        {
            ScheduledEventId = 1,
            GuildId = 111,
            ChannelId = 222,
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            Day = 5,
            Hour = 4,
            RepeatIntervalDays = 4,
            UtcEventTime = Now.AddDays(-4)
        };

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(6, ev.Day);
        Assert.True(ev.UtcEventTime > Now);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_Interval4_TwoCyclesBehind_DayAdvancesTwice()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        // Day=5, interval=4: Now-8 → Now-4 → Now (still <=Now) → Now+4; three advances → Day=(5+4+4+4)%7=3
        var ev = new ScheduledEvent
        {
            ScheduledEventId = 1,
            GuildId = 111,
            ChannelId = 222,
            EventType = (short)ScheduledEventTypeEnum.RaidSignup,
            Day = 5,
            Hour = 4,
            RepeatIntervalDays = 4,
            UtcEventTime = Now.AddDays(-8)
        };

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(3, ev.Day);
        Assert.True(ev.UtcEventTime > Now);
    }

    // -------------------------------------------------------------------------
    // FastForwardEventIfBehind — DB interaction
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FastForwardEventIfBehind_WhenAdvanced_CallsUpdateAsyncOnce()
    {
        var entityService = new FakeEntityService();
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now.AddDays(-7));

        await service.FastForwardEventIfBehind(ev, Now);

        Assert.Equal(1, entityService.FakeScheduledEvent.UpdateCallCount);
        Assert.Equal(ev.ScheduledEventId, entityService.FakeScheduledEvent.LastUpdated!.ScheduledEventId);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_DbUpdateThrows_InMemoryStateIsStillCorrect()
    {
        var entityService = new FakeEntityService();
        entityService.FakeScheduledEvent.ThrowOnUpdate = new InvalidOperationException("db exploded");
        var service = BuildService(entityService);
        var originalTime = Now.AddDays(-7);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: originalTime);

        // Should not throw
        await service.FastForwardEventIfBehind(ev, Now);

        // In-memory state is still correctly advanced (Now - 7 → Now → Now + 7, since == Now is still <= Now)
        Assert.Equal(Now.AddDays(7), ev.UtcEventTime);
        Assert.True(ev.UtcEventTime > Now);
    }

    [Fact]
    public async Task FastForwardEventIfBehind_DbUpdateThrows_DoesNotPropagateException()
    {
        var entityService = new FakeEntityService();
        entityService.FakeScheduledEvent.ThrowOnUpdate = new Exception("db is down");
        var service = BuildService(entityService);
        var ev = MakeWeeklyEvent(day: 5, hour: 4, utcEventTime: Now.AddDays(-7));

        var exception = await Record.ExceptionAsync(() => service.FastForwardEventIfBehind(ev, Now));

        Assert.Null(exception);
    }
}

// -----------------------------------------------------------------------------
// Fakes
// -----------------------------------------------------------------------------

internal class FakeEntityService : IEntityService
{
    public FakeScheduledEventService FakeScheduledEvent { get; } = new();

    public IDatabaseUpdateService<ScheduledEvent> ScheduledEvent => FakeScheduledEvent;

    public IDatabaseUpdateService<Account> Account => throw new NotImplementedException();
    public IDatabaseUpdateService<FightLog> FightLog => throw new NotImplementedException();
    public IDatabaseUpdateService<FightLogRawData> FightLogRawData => throw new NotImplementedException();
    public IDatabaseUpdateService<FightsReport> FightsReport => throw new NotImplementedException();
    public IDatabaseUpdateService<Guild> Guild => throw new NotImplementedException();
    public IDatabaseUpdateService<GuildQuote> GuildQuote => throw new NotImplementedException();
    public IDatabaseUpdateService<GuildWarsAccount> GuildWarsAccount => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerFightLog> PlayerFightLog => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerFightLogMechanic> PlayerFightLogMechanic => throw new NotImplementedException();
    public IDatabaseUpdateService<PlayerRaffleBid> PlayerRaffleBid => throw new NotImplementedException();
    public IDatabaseUpdateService<Raffle> Raffle => throw new NotImplementedException();
    public IDatabaseUpdateService<SteamAccount> SteamAccount => throw new NotImplementedException();
    public IDatabaseUpdateService<RotationAnomaly> RotationAnomaly => throw new NotImplementedException();
}

internal class FakeScheduledEventService : IDatabaseUpdateService<ScheduledEvent>
{
    public int UpdateCallCount { get; private set; }
    public ScheduledEvent? LastUpdated { get; private set; }
    public Exception? ThrowOnUpdate { get; set; }
    public short RepeatIntervalDays { get; set; } = 7;

    public Task UpdateAsync(ScheduledEvent entity)
    {
        if (ThrowOnUpdate != null)
        {
            throw ThrowOnUpdate;
        }

        UpdateCallCount++;
        LastUpdated = entity;
        return Task.CompletedTask;
    }

    public Task<List<ScheduledEvent>> GetAllAsync() => Task.FromResult(new List<ScheduledEvent>());

    public Task AddAsync(ScheduledEvent entity) => Task.CompletedTask;
    public Task AddRangeAsync(List<ScheduledEvent> entity) => Task.CompletedTask;
    public Task UpdateRangeAsync(List<ScheduledEvent> entity) => Task.CompletedTask;
    public Task DeleteAsync(ScheduledEvent entity) => Task.CompletedTask;
    public Task DeleteRangeAsync(List<ScheduledEvent> entity) => Task.CompletedTask;
    public Task<bool> IfAnyAsync(Expression<Func<ScheduledEvent, bool>> predicate) => Task.FromResult(false);
    public Task<ScheduledEvent?> GetFirstOrDefaultAsync(Expression<Func<ScheduledEvent, bool>> predicate) => Task.FromResult<ScheduledEvent?>(null);
    public Task<List<ScheduledEvent>> GetWhereAsync(Expression<Func<ScheduledEvent, bool>> predicate) => Task.FromResult(new List<ScheduledEvent>());
}
