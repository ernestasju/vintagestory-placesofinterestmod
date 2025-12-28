using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Vintagestory.Client.NoObf;

namespace PlacesOfInterestMod;

public sealed class TagPattern : IEquatable<TagPattern?>
{
    private readonly TagPatternType _type;
    private readonly string _value;

    public TagPattern(
        TagPatternType type,
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _type = type;
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
        return _type switch
        {
            TagPatternType.Regex => $"/{_value}/",
            _ => $"~{_value}",
        };
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

        switch (_type)
        {
            case TagPatternType.Wildcard:
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
            case TagPatternType.Regex:
                return Regex.IsMatch(
                    tagName.Value,
                    _value,
                    RegexOptions.IgnoreCase);
            case TagPatternType.Constant:
                return string.Equals(
                    tagName.Value,
                    _value,
                    StringComparison.OrdinalIgnoreCase);
            case TagPatternType.None:
                return false;
            default:
                throw new UnreachableException("Unreachable due to exhaustive check.");
        }
    }

    public static TagPatternType DetectPatternType(string input)
    {
        return input switch
        {
            _ when Regex.IsMatch(input, @"^~(?:\\\*|\\\?|\\\\|[^*?\\])*[*?]") => TagPatternType.Wildcard,
            _ when input.StartsWith('~') => TagPatternType.Constant,
            _ when input.StartsWith('/') && input.EndsWith('/') && input.IsValidRegexPattern() => TagPatternType.Regex,
            _ => TagPatternType.None,
        };
    }

    public static string Unquote(string input)
    {
        return DetectPatternType(input) switch
        {
            TagPatternType.Wildcard => input[1..],
            TagPatternType.Regex => input[1..^1],
            TagPatternType.Constant => input[1..],
            TagPatternType.None => input,
            _ => throw new UnreachableException("Unreachable due to exhaustive check."),
        };
    }
}
