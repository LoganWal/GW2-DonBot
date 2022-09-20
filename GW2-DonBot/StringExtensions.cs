using System;

public static class StringExtensions
{    
    public static string PadCenter(this string str, int length)
    {
        return str.PadLeft(((length - str.Length) / 2 + str.Length)).PadRight(length);
    }
}