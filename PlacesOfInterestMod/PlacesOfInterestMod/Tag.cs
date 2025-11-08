using ProtoBuf;

namespace PlacesOfInterestMod;

[ProtoContract]
public class Tag
{
    [ProtoMember(1)]
    public required string Name { get; init; }

    [ProtoMember(2)]
    public int StartDay { get; set; }

    [ProtoMember(3)]
    public int EndDay { get; set; }

    public bool IsActive(int day)
    {
        if (StartDay >= 0 && day < StartDay)
        {
            return false;
        }

        if (EndDay > 0 && day > EndDay)
        {
            return false;
        }

        return true;
    }
}
