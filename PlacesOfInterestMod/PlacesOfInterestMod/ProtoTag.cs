using System;
using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public sealed class ProtoTag
{
    [ProtoMember(1)]
    public required string? Name { get; init; }

    [ProtoMember(2)]
    public int StartDay { get; set; }

    [ProtoMember(3)]
    public int EndDay { get; set; }

    public static implicit operator Tag?(ProtoTag? tag)
    {
        if (tag is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(tag.Name))
        {
            return null;
        }

        return new()
        {
            Name = new(tag.Name),
            StartDay = tag.StartDay,
            EndDay = tag.EndDay,
        };
    }

    public static implicit operator ProtoTag(Tag tag)
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
}
