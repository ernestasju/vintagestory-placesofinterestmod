using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class PlayerPlacesOfInterest : IPlayerPlacesOfInterest
{
    private readonly IPlayer _player;
    private PlayerPlaces? _places;
    private PlayerCalendar? _calendar;

    public PlayerPlacesOfInterest(IPlayer player)
    {
        _player = player;
    }

    public IPlayer Player => _player;

    public PlayerPlaces Places => _places ??= PlayerPlaces.Load(this);

    public PlayerCalendar Calendar => _calendar ??= new(this);

    public Vec3d XYZ => _player.Entity.Pos.XYZ;

    public Vec3i RoughXYZ => PlayerPlaces.ToRoughPlace(_player.Entity.Pos.XYZ);

    public Vec2d XZ => _player.Entity.Pos.XYZ.ToXZ();

    public void RemoveModData(string key)
    {
        _player.WorldData.RemoveModdata(key);
    }

    public T LoadModData<T>(string key, T defaultValue)
    {
        return _player.WorldData.GetModData(key, defaultValue);
    }

    public void SaveModData<T>(string key, T value)
    {
        _player.WorldData.SetModData(key, value);
    }
}
