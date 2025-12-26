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

    public string QuotedString => $@"""{_value}""";

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
        if (_value.Contains('*') || _value.Contains('?') || _value.Contains('\\') || _value.Contains('"'))
        {
            return QuotedString;
        }

        return _value;
    }

    public bool Matches(TagPattern tagPattern)
    {
        ArgumentNullException.ThrowIfNull(tagPattern);
        return tagPattern.Test(this);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TagName);
    }

    public bool Equals(TagName? other)
    {
        return other is not null && _value == other._value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_value);
    }
}
