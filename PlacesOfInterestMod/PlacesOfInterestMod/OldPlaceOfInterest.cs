using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

[ProtoContract]
public class OldPlaceOfInterest
{
    [ProtoMember(1)]
    public required Vec3d XYZ { get; init; }

    [ProtoMember(2)]
    public required HashSet<string> Tags { get; init; }
}
