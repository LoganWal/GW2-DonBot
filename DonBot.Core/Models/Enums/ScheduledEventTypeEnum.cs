namespace DonBot.Models.Enums;

public enum ScheduledEventTypeEnum : short
{
    RaidSignup = 0,
    WvwLeaderboard = 1,
    PveLeaderboard = 2,
    // 3 was Wordle; do not reuse it because old database snapshots may contain it.
    // 4 is legacy WvW raid signup. New signups use RaidSignup with response options.
    WvwRaidSignup = 4
}
