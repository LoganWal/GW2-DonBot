namespace DonBot.Extensions;

public static class IntExtensions
{
    public static string GetFightModeName(this int fightMode)
    {
        return fightMode switch
        {
            0 => "NM",
            1 => "CM",
            2 => "LCM",
            _ => "NM"
        };
    }
}