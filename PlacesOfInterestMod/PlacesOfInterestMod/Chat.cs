using System.Collections.Generic;
using System.Linq;

namespace PlacesOfInterestMod;

public static class Chat
{
    public static string FormTagsText(
        IEnumerable<TagName> includedTagNames,
        IEnumerable<TagPattern> includedTagPatterns,
        IEnumerable<TagName> excludedTagNames,
        IEnumerable<TagPattern> excludedTagPatterns)
    {
        string[] tags =
            [
                .. includedTagNames.OrderBy(x => x.Value.ToLowerInvariant()).Select(x => x.ToString()),
                .. includedTagPatterns.OrderBy(x => x.ToString().ToLowerInvariant()).Select(x => x.ToString()),
                .. excludedTagNames.OrderBy(x => x.Value.ToLowerInvariant()).Select(x => "-" + x),
                .. excludedTagPatterns.OrderBy(x => x.ToString().ToLowerInvariant()).Select(x => "-" + x),
            ];

        return string.Join(" ", tags);
    }
}
