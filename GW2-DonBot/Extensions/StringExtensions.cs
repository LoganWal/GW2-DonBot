namespace DonBot.Extensions;

public static class StringExtensions
{
    public static string ClipAt(this string str, int length) =>
        str[..Math.Min(str.Length, length)];

    public static string PadCenter(this string str, int length, char paddingChar = ' ') =>
        str.PadLeft((length - str.Length) / 2 + str.Length, paddingChar).PadRight(length, paddingChar);

    public static string FormatNumber(this float number, float referenceNumber) => referenceNumber switch
    {
        > 1000000000.0f => $"{number / 1000000000.0f:F1}B",
        > 1000000.0f => $"{number / 1000000.0f:F1}M",
        > 1000.0f => $"{number / 1000.0f:F1}K",
        _ => number.ToString("F1")
    };

    public static string FormatNumber(this float number, bool asPerSec = false)
    {
        var suffix = asPerSec ? "/s" : string.Empty;
        return number switch
        {
            > 1000000000.0f => $"{number / 1000000000.0f:F1}B{suffix}",
            > 1000000.0f => $"{number / 1000000.0f:F1}M{suffix}",
            > 1000.0f => $"{number / 1000.0f:F1}K{suffix}",
            _ => $"{number:F1}{suffix}"
        };
    }

    public static string FormatNumber(this long number) =>
        ((float)number).FormatNumber();

    public static string FormatNumber(this int number) =>
        ((float)number).FormatNumber();

    public static string FormatSimplePercentage(this float number) => $"{number:F0}%";

    public static string FormatPercentage(this double number) => ((float)number).FormatPercentage();

    public static float TimeToSeconds(this string timeString)
    {
        var splits = timeString.Split(' ');
        var time = 0.0f;
        foreach (var split in splits)
        {
            if (split.EndsWith("ms"))
            {
                time += float.Parse(split[..^2]) * 0.001f;
            }
            else if (split.EndsWith("s"))
            {
                time += float.Parse(split[..^1]);
            }
            else if (split.EndsWith("m"))
            {
                time += float.Parse(split[..^1]) * 60;
            }
            else if (split.EndsWith("hr"))
            {
                time += float.Parse(split[..^2]) * 60 * 60;
            }
        }
        return time;
    }
    
    private static string FormatPercentage(this float number) => $"{number:F1}%";
}