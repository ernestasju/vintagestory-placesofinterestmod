using System.Collections.Generic;
using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public class LoadedPlacesPacket
{
    [ProtoMember(1)]
    public required List<PlaceOfInterest> Places { get; init; }
}
