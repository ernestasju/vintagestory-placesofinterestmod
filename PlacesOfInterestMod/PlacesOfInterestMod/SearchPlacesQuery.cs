using System.Collections.Generic;
using System.Linq;

namespace PlacesOfInterestMod;

public sealed class SearchPlacesQuery
{
    private readonly TagQuery _tagQuery;

    private SearchPlacesQuery(
        TagQuery tagQuery)
    {
        _tagQuery = tagQuery;
    }

    public IEnumerable<TagName> TagNamesToInclude => _tagQuery.IncludedTagNames;

    public IEnumerable<TagPattern> TagPatternsToInclude => _tagQuery.IncludedTagPatterns;

    public IEnumerable<TagName> TagNamesToExclude => _tagQuery.ExcludedTagNames;

    public IEnumerable<TagPattern> TagPatternsToExclude => _tagQuery.ExcludedTagPatterns;

    public void SearchPlaces(
        List<Place> places,
        out List<Place> matchingPlaces)
    {
        matchingPlaces = places.Where(x => _tagQuery.TestPlace(x)).ToList();
    }

    public static SearchPlacesQuery Parse(string input, int day)
    {
        TagQuery tagQuery = TagQuery.Parse(input, day);

        return new(tagQuery);
    }
}
