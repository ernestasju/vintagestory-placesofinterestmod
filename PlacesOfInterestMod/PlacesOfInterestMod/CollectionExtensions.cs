using System.Collections.Generic;
using System.Linq;

namespace PlacesOfInterestMod;

public static class CollectionExtensions
{
    public static bool ContainsAll(
        this ICollection<string> collection,
        ICollection<string> subcollection,
        bool caseInsensitive = true,
        bool wildcardMatching = true)
    {
#pragma warning disable T0010 // Internal Styling Rule T0010
        return subcollection
            .All(subItem =>
                collection.Any(item =>
                    item.MatchesPattern(
                        subItem,
                        caseInsensitive: caseInsensitive,
                        wildcardMatching: wildcardMatching)));
#pragma warning restore T0010 // Internal Styling Rule T0010
    }

    public static bool ContainsAny(
        this ICollection<string> collection,
        ICollection<string> subcollection,
        bool caseInsensitive = true,
        bool wildcardMatching = true)
    {
#pragma warning disable T0010 // Internal Styling Rule T0010
        return subcollection
            .Any(item =>
                collection.Any(x =>
                    x.MatchesPattern(
                        item,
                        caseInsensitive: caseInsensitive,
                        wildcardMatching: wildcardMatching)));
#pragma warning restore T0010 // Internal Styling Rule T0010
    }
}
