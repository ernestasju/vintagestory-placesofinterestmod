using System;
using System.Text.RegularExpressions;

namespace PlacesOfInterestMod;

public static class StringExtensions
{
    public static bool IsValidRegexPattern(this string value)
    {
        try
        {
            _ = new Regex(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
