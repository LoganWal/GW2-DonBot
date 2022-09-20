using System;

public static class StringExtensions
{    
    public static string PadCenter(this string str, int length)
    {
        return str.PadLeft(((length - str.Length) / 2 + str.Length)).PadRight(length);
    }

    public static string FormatNumber(this float number, float referenceNumber)
    {
        if (referenceNumber > 1000000000.0f)
        {
            return $"{(number / 1000000000.0f).ToString("F1")}B";
        }
        else if (referenceNumber > 1000000.0f)
        {
            return $"{(number / 1000000.0f).ToString("F1")}M";
        }
        else if (referenceNumber > 1000.0f)
        {
            return $"{(number / 1000.0f).ToString("F1")}K";
        }
        else
        {
            return number.ToString("F1");
        }
    }

    public static string FormatNumber(this long number, long referenceNumber)
    {
        return ((float)number).FormatNumber(referenceNumber);
    }

    public static string FormatNumber(this int number, int referenceNumber)
    {
        return ((float)number).FormatNumber(referenceNumber);
    }

    public static string FormatNumber(this float number)
    {
        if (number > 1000000000.0f)
        {
            return $"{(number / 1000000000.0f).ToString("F1")}B";
        }
        else if (number > 1000000.0f)
        {
            return $"{(number / 1000000.0f).ToString("F1")}M";
        }
        else if (number > 1000.0f)
        {
            return $"{(number / 1000.0f).ToString("F1")}K";
        }
        else
        {
            return number.ToString("F1");
        }
    }

    public static string FormatNumber(this long number)
    {
        return ((float)number).FormatNumber();
    }

    public static string FormatNumber(this int number)
    {
        return ((float)number).FormatNumber();
    }

    public static string FormatPercentage(this float number)
    {
        return $"{number.ToString("F1")}%";
    }

    public static string FormatPercentage(this double number)
    {
        return FormatPercentage((float)number);
    }

    public static float TimeToSeconds(this string timeString)
    {
        var splits = timeString.Split(' ');
        float time = 0.0f;
        for (int i = 0; i < splits.Length; i++)
        {
            if (splits[i].EndsWith("ms"))
            {
                time += float.Parse(splits[i].Substring(0, splits[i].Length - 2)) * 0.001f;
            }
            else if (splits[i].EndsWith("s"))
            {
                time += float.Parse(splits[i].Substring(0, splits[i].Length - 1));
            }
            else if (splits[i].EndsWith("m"))
            {
                time += float.Parse(splits[i].Substring(0, splits[i].Length - 1)) * 60;
            }
            else if (splits[i].EndsWith("hr"))
            {
                time += float.Parse(splits[i].Substring(0, splits[i].Length - 2)) * 60 * 60;
            }
        }
        return time;
    }
}