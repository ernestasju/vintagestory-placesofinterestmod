using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public class LoadPlacesPacket
{
    [ProtoMember(1)]
    public required int SearchRadius { get; init; }

    [ProtoMember(2)]
    public required string TagQueriesText { get; init; }
}
