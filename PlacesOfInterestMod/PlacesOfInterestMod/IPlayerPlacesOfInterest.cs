using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public interface IPlayerPlacesOfInterest
{
    PlayerCalendar Calendar { get; }
    PlayerPlaces Places { get; }
    IPlayer Player { get; }
    Vec3i RoughXYZ { get; }
    Vec3d XYZ { get; }
    Vec2d XZ { get; }

    T LoadModData<T>(string key, T defaultValue);
    void RemoveModData(string key);
    void SaveModData<T>(string key, T value);
}
