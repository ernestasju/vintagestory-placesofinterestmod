using System;
using System.Text.RegularExpressions;

namespace PlacesOfInterestMod
{
    public static class StringExtensions
    {
        public static bool MatchesPattern(
            this string value,
            string expectedValue,
            bool caseInsensitive = false,
            bool wildcardMatching = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedValue);

            if (wildcardMatching)
            {
                string pattern =
                    "^" +
                    Regex
                        .Escape(expectedValue)
                        .Replace(@"\\\*", @"[*]")
                        .Replace(@"\\\?", @"[?]")
                        .Replace(@"\*", @".*")
                        .Replace(@"\?", @".") +
                    "$";
                return Regex.IsMatch(value, pattern, caseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None);
            }

            return string.Equals(value, expectedValue, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
    }
}
