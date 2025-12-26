using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

[ProtoContract]
public class OldProtoPlace
{
    [ProtoMember(1)]
    public required Vec3d XYZ { get; init; }

    [ProtoMember(2)]
    public required HashSet<string>? Tags { get; init; }

    public static implicit operator Place?(OldProtoPlace? place)
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
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .DistinctBy(x => x.ToLowerInvariant())
            .Select(
                x => new Tag()
                {
                    Name = new(x),
                    StartDay = 0,
                    EndDay = 0,
                })
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
}
