using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public class SavedPlacesPacket
{
    [ProtoMember(1)]
    public required int PlacesCount { get; init; }
}
