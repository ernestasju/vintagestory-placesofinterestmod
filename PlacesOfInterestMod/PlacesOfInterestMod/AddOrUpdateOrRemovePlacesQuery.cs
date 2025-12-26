using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class AddOrUpdateOrRemovePlacesQuery
{
    private readonly TagQuery _tagQuery;
    private readonly ExistingPlaceAction _existingPlaceAction;

    private AddOrUpdateOrRemovePlacesQuery(
        TagQuery tagQuery,
        ExistingPlaceAction existingPlaceAction)
    {
        _tagQuery = tagQuery;
        _existingPlaceAction = existingPlaceAction;
    }

    public IEnumerable<TagName> TagNamesToInclude => _tagQuery.IncludedTagNames;

    public IEnumerable<TagName> TagNamesToExclude => _tagQuery.ExcludedTagNames;

    public IEnumerable<TagPattern> TagPatternsToExclude => _tagQuery.ExcludedTagPatterns;

    public bool HasTagNamesToInclude()
    {
        return _tagQuery.HasIncludedTagNames();
    }

    public void AddOrUpdateOrRemovePlaces(
        Places places,
        Vec3d position,
        out int numberOfRemovedPlaces,
        out int numberOfChangedPlaces,
        out int numberOfAddedPlaces)
    {
        numberOfRemovedPlaces = 0;
        numberOfChangedPlaces = 0;
        numberOfAddedPlaces = 0;

        if (places.Count == 0)
        {
            Place place = new()
            {
                XYZ = position,
                Tags = [],
            };

            _tagQuery.UpdatePlace(place, out bool _);

            if (place.Tags.Count > 0)
            {
                numberOfAddedPlaces += 1;
                places.AddToPlayerPlaces(place);
            }

            return;
        }

        if (_existingPlaceAction == ExistingPlaceAction.Skip)
        {
            return;
        }

        foreach (Place place in places)
        {
            _tagQuery.UpdatePlace(place, out bool tagsChanged);

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

    public static AddOrUpdateOrRemovePlacesQuery Parse(string input, int day)
    {
        TagQuery tagQuery = TagQuery.Parse(input, day);

        return new(tagQuery, ExistingPlaceAction.Replace);
    }

    public static AddOrUpdateOrRemovePlacesQuery CreateForUpdate(
        IEnumerable<TagName> tagNamesToInclude,
        IEnumerable<TagName> tagNamesToExclude,
        ExistingPlaceAction existingPlaceAction,
        int day,
        int startDay,
        int endDay)
    {
        TagQuery tagQuery = TagQuery.CreateForUpdate(
            tagNamesToInclude,
            tagNamesToExclude,
            existingPlaceAction == ExistingPlaceAction.Replace,
            day,
            startDay,
            endDay);

        return new(tagQuery, existingPlaceAction);
    }
}
