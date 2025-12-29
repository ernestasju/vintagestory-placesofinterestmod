using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class PlayerPlacesOfInterest : IPlayerPlacesOfInterest
{
    private readonly IVintageStoryPlayer _player;
    private PlayerPlaces? _places;
    private PlayerCalendar? _calendar;

    public PlayerPlacesOfInterest(IPlayer player)
        : this(new VintageStoryPlayer(player))
    {
    }

    public PlayerPlacesOfInterest(IVintageStoryPlayer player)
    {
        _player = player;
    }

    public IVintageStoryPlayer Player => _player;

    public PlayerPlaces Places => _places ??= PlayerPlaces.Load(this);

    public PlayerCalendar Calendar => _calendar ??= new(this);

    public Vec3d XYZ => _player.XYZ;

    public Vec3i RoughXYZ => PlayerPlaces.ToRoughPlace(XYZ);

    public Vec2d XZ => XYZ.ToXZ();
}
