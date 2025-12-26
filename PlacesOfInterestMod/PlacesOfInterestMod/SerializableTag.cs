using System;

namespace PlacesOfInterestMod;

public sealed class SerializableTag
{
    public required string? Name { get; init; }

    public required int StartDay { get; init; }

    public required int EndDay { get; init; }

    public static implicit operator SerializableTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(tag.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag.Name.Value);

        return new()
        {
            Name = tag.Name.Value,
            StartDay = tag.StartDay,
            EndDay = tag.EndDay,
        };
    }

    public static implicit operator Tag(SerializableTag serializableTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serializableTag.Name);

        return new()
        {
            Name = new TagName(serializableTag.Name),
            StartDay = serializableTag.StartDay,
            EndDay = serializableTag.EndDay,
        };
    }
}
