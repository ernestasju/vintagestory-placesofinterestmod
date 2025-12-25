using System.Collections.Generic;
using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public class SavePlacesPacket
{
    [ProtoMember(1)]
    public required ExistingPlaceAction ExistingPlaceAction { get; init; }

    [ProtoMember(2)]
    public required List<PlaceOfInterest> Places { get; init; }
}
