using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod.IntegrationTests;

public sealed class TestPlayerData
{
    public Vec3d XYZ { get; set; } = new(10, 20, 30);

    public List<ProtoPlace> ProtoPlaces { get; set; } = [];

    public void AddPlace(Vec3d xyz, params string[] tags)
    {
        ProtoPlaces.Add(
            new ProtoPlace()
            {
                XYZ = xyz,
                Tags = tags.Select(x => new ProtoTag() { Name = x, StartDay = 0, EndDay = 0 }).ToList(),
            });
    }
}
