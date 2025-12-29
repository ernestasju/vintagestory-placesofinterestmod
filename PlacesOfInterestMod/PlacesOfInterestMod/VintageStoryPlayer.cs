using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class VintageStoryPlayer : IVintageStoryPlayer
{
    private readonly IPlayer _player;

    public VintageStoryPlayer(IPlayer player)
    {
        _player = player;
    }

    public IGameCalendar Calendar => _player.Entity.World.Calendar;

    public Vec3d XYZ => _player.Entity.Pos.XYZ;

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
