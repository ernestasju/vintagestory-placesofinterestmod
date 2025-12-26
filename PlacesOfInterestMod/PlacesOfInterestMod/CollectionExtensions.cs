using System;
using System.Collections.Generic;
using System.Linq;

namespace PlacesOfInterestMod;

public static class CollectionExtensions
{
    public static IEnumerable<TTarget> SelectNonNulls<TSource, TTarget>(
        this IEnumerable<TSource> source,
        Func<TSource, TTarget?> func)
    {
        return source
            .Select(x => func(x))
            .Where(x => x is not null)
            .Select(x => x!);
    }
}
