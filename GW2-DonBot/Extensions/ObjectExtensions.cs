namespace DonBot.Extensions;

public static class ObjectExtensions
{
    public static long? TryParseLong(this object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return (long)value;
        }
        catch
        {
            return 0;
        }
    }
}