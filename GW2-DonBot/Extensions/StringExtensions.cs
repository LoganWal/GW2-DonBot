namespace Extensions
{
    public static class StringExtensions
    {
        public static string ClipAt(this string str, int length)
        {
            return str.Substring(0, Math.Min(str.Length, length));
        }

        public static string PadCenter(this string str, int length, char paddingChar = ' ')
        {
            return str.PadLeft(((length - str.Length) / 2 + str.Length), paddingChar).PadRight(length, paddingChar);
        }

        public static string FormatNumber(this float number, float referenceNumber)
        {
            if (referenceNumber > 1000000000.0f)
            {
                return $"{(number / 1000000000.0f):F1}B";
            }
            else if (referenceNumber > 1000000.0f)
            {
                return $"{(number / 1000000.0f):F1}M";
            }
            else if (referenceNumber > 1000.0f)
            {
                return $"{(number / 1000.0f):F1}K";
            }
            else
            {
                return number.ToString("F1");
            }
        }

        public static string FormatNumber(this float number)
        {
            if (number > 1000000000.0f)
            {
                return $"{(number / 1000000000.0f):F1}B";
            }
            else if (number > 1000000.0f)
            {
                return $"{(number / 1000000.0f):F1}M";
            }
            else if (number > 1000.0f)
            {
                return $"{(number / 1000.0f):F1}K";
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
            return $"{number:F1}%";
        }

        public static string FormatSimplePercentage(this float number)
        {
            return $"{number:F0}%";
        }

        public static string FormatPercentage(this double number)
        {
            return FormatPercentage((float)number);
        }

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

        public static string ReplaceSpacesWithNonBreaking(this string text)
        {
            return text.Replace(" ", "\u00A0");
        }
    }
}