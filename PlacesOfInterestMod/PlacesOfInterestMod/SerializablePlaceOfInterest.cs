using System.Linq;

namespace PlacesOfInterestMod;

public sealed class SerializablePlaceOfInterest
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Z { get; init; }

    public required SerializableTag[] Tags { get; init; }

    public static implicit operator SerializablePlaceOfInterest(PlaceOfInterest poi)
    {
        return new()
        {
            X = poi.XYZ.X,
            Y = poi.XYZ.Y,
            Z = poi.XYZ.Z,
            Tags = poi.Tags.Select(x => (SerializableTag)x).ToArray(),
        };
    }

    public static implicit operator PlaceOfInterest(SerializablePlaceOfInterest serializablePoi)
    {
        return new()
        {
            XYZ = new(serializablePoi.X, serializablePoi.Y, serializablePoi.Z),
            Tags = serializablePoi.Tags.Select(x => (Tag)x).ToList(),
        };
    }
}
