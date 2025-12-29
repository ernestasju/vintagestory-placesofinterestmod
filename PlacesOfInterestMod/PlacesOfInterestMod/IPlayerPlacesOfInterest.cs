using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public interface IPlayerPlacesOfInterest
{
    PlayerCalendar Calendar { get; }

    PlayerPlaces Places { get; }

    IVintageStoryPlayer Player { get; }

    Vec3i RoughXYZ { get; }

    Vec3d XYZ { get; }

    Vec2d XZ { get; }
}
