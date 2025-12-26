using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

[ProtoContract]
public sealed class ProtoPlace
{
    [ProtoMember(1)]
    public required Vec3d XYZ { get; init; }

    [ProtoMember(2)]
    public required List<ProtoTag>? Tags { get; init; }

    public static implicit operator Place?(ProtoPlace? place)
    {
        if (place is null)
        {
            return null;
        }

        if (place.Tags is null)
        {
            return null;
        }

        List<Tag> tags = place.Tags
            .SelectNonNulls(x => (Tag?)x)
            .DistinctBy(x => x.Name)
            .ToList();

        if (tags.Count == 0)
        {
            return null;
        }

        return new()
        {
            XYZ = place.XYZ,
            Tags = tags,
        };
    }

    public static implicit operator ProtoPlace(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);

        return new()
        {
            XYZ = place.XYZ,
            Tags = place.Tags
                .Select(
                    x =>
                    {
                        ArgumentNullException.ThrowIfNull(x);
                        return (ProtoTag)x;
                    })
                .ToList(),
        };
    }
}
