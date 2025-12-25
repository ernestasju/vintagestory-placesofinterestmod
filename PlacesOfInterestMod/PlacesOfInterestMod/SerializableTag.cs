namespace PlacesOfInterestMod;

public sealed class SerializableTag
{
    public required string Name { get; init; }

    public required int StartDay { get; init; }

    public required int EndDay { get; init; }

    public static implicit operator SerializableTag(Tag tag)
    {
        return new()
        {
            Name = tag.Name,
            StartDay = tag.StartDay,
            EndDay = tag.EndDay,
        };
    }

    public static implicit operator Tag(SerializableTag serializableTag)
    {
        return new()
        {
            Name = serializableTag.Name,
            StartDay = serializableTag.StartDay,
            EndDay = serializableTag.EndDay,
        };
    }
}
