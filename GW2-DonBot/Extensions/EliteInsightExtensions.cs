namespace DonBot.Extensions;

public static class EliteInsightExtensions
{
    public static string GetClassAppend(string? className)
    {
        return string.IsNullOrEmpty(className) ? string.Empty : $" ({className[..Math.Min(3, className.Length)]})";
    }
}
