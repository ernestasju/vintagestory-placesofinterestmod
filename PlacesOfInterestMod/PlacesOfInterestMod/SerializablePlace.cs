using System;
using System.Linq;

namespace PlacesOfInterestMod;

public sealed class SerializablePlace
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required double Z { get; init; }

    public required SerializableTag[]? Tags { get; init; }

    public static implicit operator SerializablePlace(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);

        return new()
        {
            X = place.XYZ.X,
            Y = place.XYZ.Y,
            Z = place.XYZ.Z,
            Tags = place.Tags
                .Select(
                    x =>
                    {
                        ArgumentNullException.ThrowIfNull(x);
                        return (SerializableTag)x;
                    })
                .ToArray(),
        };
    }

    public static implicit operator Place(SerializablePlace place)
    {
        return new()
        {
            XYZ = new(place.X, place.Y, place.Z),
            Tags = (place.Tags ?? [])
                .Select(
                    x =>
                    {
                        ArgumentNullException.ThrowIfNull(x);
                        return (Tag)x;
                    })
                .ToList(),
        };
    }
}
