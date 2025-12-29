using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public interface IVintageStoryPlayer
{
    IGameCalendar Calendar { get; }

    Vec3d XYZ { get; }

    T LoadModData<T>(string key, T defaultValue);

    void RemoveModData(string key);

    void SaveModData<T>(string key, T value);
}