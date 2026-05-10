namespace DonBot.Models.Enums;

public enum ScheduledEventTypeEnum : short
{
    RaidSignup = 0,
    WvwLeaderboard = 1,
    PveLeaderboard = 2,
    // 3 was Wordle (removed); do not reuse, historical rows may still exist in older DB snapshots.
    WvwRaidSignup = 4
}
