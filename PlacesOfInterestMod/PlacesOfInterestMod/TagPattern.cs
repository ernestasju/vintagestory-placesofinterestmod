using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlacesOfInterestMod;

public sealed class TagPattern : IEquatable<TagPattern?>
{
    private readonly string _value;

    public TagPattern(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public static bool operator ==(TagPattern? left, TagPattern? right)
    {
        return EqualityComparer<TagPattern>.Default.Equals(left, right);
    }

    public static bool operator !=(TagPattern? left, TagPattern? right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return $"~{_value}";
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TagPattern);
    }

    public bool Equals(TagPattern? other)
    {
        return other is not null && _value == other._value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_value);
    }

    public bool Test(
        TagName tagName)
    {
        ArgumentNullException.ThrowIfNull(tagName);

        string pattern =
            "^" +
            Regex
                .Escape(_value)
                .Replace(@"\\\*", @"[*]")
                .Replace(@"\\\?", @"[?]")
                .Replace(@"\\\\", @"[\\]")
                .Replace(@"\*", @".*")
                .Replace(@"\?", @".") +
            "$";

        return Regex.IsMatch(
            tagName.Value,
            pattern,
            RegexOptions.IgnoreCase);
    }

    public static bool IsPattern(string input)
    {
        return input.StartsWith('~');
    }

    public static bool IsWildcardPattern(string input)
    {
        return Regex.IsMatch(input, @"^~(?:\\\*|\\\?|\\\\|[^*?\\])*[*?]");
    }

    public static string Unquote(string input)
    {
        return input[1..];
    }
}
