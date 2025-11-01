using System.Collections.Generic;
using System.Linq;

namespace PlacesOfInterestMod
{
    public static class CollectionExtensions
    {
        public static bool ContainsAll(
            this ICollection<string> collection,
            ICollection<string> subcollection,
            bool caseInsensitive = true,
            bool wildcardMatching = true)
        {
            return subcollection
                .All(item =>
                    collection.Any(x =>
                        x.MatchesPattern(
                            item,
                            caseInsensitive: caseInsensitive,
                            wildcardMatching: wildcardMatching)));
        }

        public static bool ContainsAny(
            this ICollection<string> collection,
            ICollection<string> subcollection,
            bool caseInsensitive = true,
            bool wildcardMatching = true)
        {
            return subcollection
                .Any(item =>
                    collection.Any(x =>
                        x.MatchesPattern(
                            item,
                            caseInsensitive: caseInsensitive,
                            wildcardMatching: wildcardMatching)));
        }
    }
}
