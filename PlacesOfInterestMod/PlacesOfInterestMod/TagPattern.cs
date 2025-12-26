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

    public override string ToString()
    {
        return _value;
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

#pragma warning disable SA1201 // Elements should appear in the correct order
    public static bool operator ==(TagPattern? left, TagPattern? right)
    {
        return EqualityComparer<TagPattern>.Default.Equals(left, right);
    }

    public static bool operator !=(TagPattern? left, TagPattern? right)
    {
        return !(left == right);
    }
#pragma warning restore SA1201 // Elements should appear in the correct order
}
