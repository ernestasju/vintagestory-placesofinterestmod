using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public enum ExistingPlaceAction
{
    Skip = 0,
    Update = 1,
    Replace = 2,
}
