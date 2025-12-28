using Vintagestory.API.Config;

namespace PlacesOfInterestMod;

public sealed record LocalizedText(string Key, params object[] Args)
{
    public static implicit operator string(LocalizedText text)
    {
        return Lang.Get(text.Key, text.Args);
    }
}
