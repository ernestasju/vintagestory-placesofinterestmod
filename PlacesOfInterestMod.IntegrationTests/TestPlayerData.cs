using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod.IntegrationTests;

public sealed class TestPlayerData
{
    private static readonly Random _random = new();

    public TestPlayerData()
    {
        double x = _random.Next(-1000, 1001);
        double y = _random.Next(80, 121);
        double z = _random.Next(-1000, 1001);
        StartingXYZ = new Vec3d(x, y, z);
        XYZ = StartingXYZ;
    }

    /// <summary>
    /// Gets starting player position.
    /// </summary>
    public Vec3d StartingXYZ { get; }

    /// <summary>
    /// Gets current player position.
    /// </summary>
    public Vec3d XYZ { get; private set; }

    /// <summary>
    /// Gets or sets places known to the player.
    /// </summary>
    public List<ProtoPlace> ProtoPlaces { get; set; } = [];

    public Vec3d Place(double dx, double dy, double dz)
    {
        return StartingXYZ + new Vec3d(dx, dy, dz);
    }

    public void MoveToDifferentPlace(double dx, double dy, double dz)
    {
        XYZ += new Vec3d(dx, dy, dz);
    }

    /// <summary>
    /// Changes player position to a random point inside the current place.
    /// </summary>
    public void MoveAroundThePlace()
    {
        XYZ =
            XYZ.ToRoughPlace(PlayerPlaces.RoughPlaceResolution, PlayerPlaces.RoughPlaceResolution / 2).ToVec3d() +
            _random.NextCubePoint(PlayerPlaces.RoughPlaceResolution / 2 * 0.9);
    }

    /// <summary>
    /// Changes player position to a random point on the boundary of the current place.
    /// </summary>
    public void MoveToPlaceBoundary()
    {
        XYZ =
            XYZ.ToRoughPlace(PlayerPlaces.RoughPlaceResolution, PlayerPlaces.RoughPlaceResolution / 2).ToVec3d() +
            _random.NextCubeSurfacePoint(PlayerPlaces.RoughPlaceResolution / 2 * 0.9);
    }

    /// <summary>
    /// Adds a place with given tags at given position.
    /// </summary>
    /// <param name="xyz">Position.</param>
    /// <param name="tags">Tags.</param>
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
