using System;
using System.Collections.Generic;

namespace PlacesOfInterestMod;

public sealed class SearchAndUpdateOrRemovePlacesQuery
{
    private readonly TagQuery _searchTagQuery;
    private readonly TagQuery _updateTagQuery;

    private SearchAndUpdateOrRemovePlacesQuery(
        TagQuery searchTagQuery,
        TagQuery updateTagQuery)
    {
        _searchTagQuery = searchTagQuery;
        _updateTagQuery = updateTagQuery;
    }

    public IEnumerable<TagName> SearchTagNamesToInclude => _searchTagQuery.IncludedTagNames;

    public IEnumerable<TagPattern> SearchTagPatternsToInclude => _searchTagQuery.IncludedTagPatterns;

    public IEnumerable<TagName> SearchTagNamesToExclude => _searchTagQuery.ExcludedTagNames;

    public IEnumerable<TagPattern> SearchTagPatternsToExclude => _searchTagQuery.ExcludedTagPatterns;

    public IEnumerable<TagName> UpdateTagNamesToInclude => _updateTagQuery.IncludedTagNames;

    public IEnumerable<TagPattern> UpdateTagPatternsToInclude => _updateTagQuery.IncludedTagPatterns;

    public IEnumerable<TagName> UpdateTagNamesToExclude => _updateTagQuery.ExcludedTagNames;

    public IEnumerable<TagPattern> UpdateTagPatternsToExclude => _updateTagQuery.ExcludedTagPatterns;

    public void SearchAndUpdatePlaces(
        Places places,
        out int numberOfFoundPlaces,
        out int numberOfRemovedPlaces,
        out int numberOfChangedPlaces)
    {
        numberOfFoundPlaces = 0;
        numberOfChangedPlaces = 0;
        numberOfRemovedPlaces = 0;

        foreach (Place place in places)
        {
            if (!_searchTagQuery.TestPlace(place))
            {
                continue;
            }

            numberOfFoundPlaces += 1;

            _updateTagQuery.UpdatePlace(place, out bool tagsChanged);
            if (tagsChanged)
            {
                if (place.Tags.Count == 0)
                {
                    numberOfRemovedPlaces += 1;
                    places.RemoveFromPlayerPlaces(place);
                }
                else
                {
                    numberOfChangedPlaces += 1;
                }
            }
        }
    }

    public static SearchAndUpdateOrRemovePlacesQuery Parse(string input, int day)
    {
        string cleanedInput = input.Trim();
        if (!input.Contains(" -> "))
        {
            cleanedInput = " -> " + cleanedInput;
        }

        string[] parts = cleanedInput.Split(" -> ", StringSplitOptions.TrimEntries);

        TagQuery searchQuery = TagQuery.Parse(parts[0], day);
        TagQuery updateQuery = TagQuery.Parse(parts[1], day);

        return new(searchQuery, updateQuery);
    }
}
