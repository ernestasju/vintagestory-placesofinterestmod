using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class Place
{
    public required Vec3d XYZ { get; init; }

    public required List<Tag> Tags { get; init; }

    public void ReplaceTags(IEnumerable<Tag> newTags)
    {
        Tags.Clear();
        Tags.AddRange(newTags);
    }

    public IEnumerable<TagName> CalculateActiveTagNames(int day)
    {
        return CalculateActiveTags(day).Select(x => x.Name);
    }

    private IEnumerable<Tag> CalculateActiveTags(int day)
    {
        return Tags.Where(x => x.IsActive(day));
    }
}