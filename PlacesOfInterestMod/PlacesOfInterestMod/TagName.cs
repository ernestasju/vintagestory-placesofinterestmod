using System;
using System.Collections.Generic;

namespace PlacesOfInterestMod;

public sealed class TagName : IEquatable<TagName?>
{
    private readonly string _value;

    public TagName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public static TagName Excluded { get; } = new("excluded");

    public static TagName Hidden { get; } = new("hidden");

    public static TagName Ignored { get; } = new("ignored");

    public string Value => _value;

    public static bool operator ==(TagName? left, TagName? right)
    {
        return EqualityComparer<TagName>.Default.Equals(left, right);
    }

    public static bool operator !=(TagName? left, TagName? right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        if (TagPattern.LooksLikePattern(_value))
        {
            return $@"""{_value}""";
        }

        return _value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TagName);
    }

    public bool Equals(TagName? other)
    {
        if (other is null)
        {
            return false;
        }

        if (IsExcluded() && other.IsExcluded())
        {
            return true;
        }

        return _value.Equals(other._value, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        // NOTE: We want excluded/hidden/ignored to be treated as the same value.
        // NOTE: For example, HashSet.Contains will first call GetHashCode to test if values are possibly equal and
        // NOTE: immediately return false if hash codes are different.
        if (IsExcluded())
        {
            return HashCode.Combine(Excluded.Value);
        }

        return HashCode.Combine(_value.ToLowerInvariant());
    }

    public bool IsExcluded()
    {
        return
            _value.Equals(Excluded.Value, StringComparison.OrdinalIgnoreCase) ||
            _value.Equals(Hidden.Value, StringComparison.OrdinalIgnoreCase) ||
            _value.Equals(Ignored.Value, StringComparison.OrdinalIgnoreCase);
    }

    public bool Matches(TagPattern tagPattern)
    {
        ArgumentNullException.ThrowIfNull(tagPattern);
        return tagPattern.Test(this);
    }

    public static bool IsName(string input)
    {
        return !input.StartsWith('~') || (input.StartsWith('"') && input.EndsWith('"'));
    }

    public static string Unquote(string input)
    {
        if (input.StartsWith("#~"))
        {
            // NOTE: Keep pattern start so we can stringify value correctly.
            return input[1..];
        }

        if (input.StartsWith('"') && input.EndsWith('"'))
        {
            return input[1..^1];
        }

        return input;
    }
}
