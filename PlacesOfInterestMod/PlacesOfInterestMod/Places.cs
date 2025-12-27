using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class Places : IEnumerable<Place>
{
    private readonly PlayerPlaces _playerPlaces;
    private readonly List<Place> _places;

    public Places(
        PlayerPlaces playerPlaces,
        IEnumerable<Place> places)
    {
        _playerPlaces = playerPlaces;
        _places = places.ToList();
    }

    public int Count => _places.Count;

    public IEnumerable<Tag> Tags =>
        _places
            .SelectMany(x => x.Tags)
            .Where(x => x.EndDay == 0 || x.EndDay >= _playerPlaces.PoI.Calendar.Today)
            .DistinctBy(x => x.Name);

    public IEnumerable<TagName> ActiveTags =>
        _places
            .SelectMany(x => x.CalculateActiveTagNames(_playerPlaces.PoI.Calendar.Today));

    public Places AtPlayerPosition()
    {
        return AtRoughPlace(PlayerPlaces.ToRoughPlace(_playerPlaces.PoI.XYZ));
    }

    public Places AtRoughPlace(Vec3i roughPlace)
    {
        return new(
            _playerPlaces,
            _places.Where(x => PlayerPlaces.ToRoughPlace(x.XYZ) == roughPlace));
    }

    public Places AroundPlayer(double radius)
    {
        return AroundPlace(_playerPlaces.PoI.XZ, radius);
    }

    public Places AroundPlace(Vec2d place, double radius)
    {
        return new(
            _playerPlaces,
            _places.Where(x => x.XYZ.ToXZ().DistanceTo(place) <= radius));
    }

    public Places Where(TagQuery tagQuery)
    {
        return Where(tagQuery.WithDays(_playerPlaces.PoI.Calendar));
    }

    public Places Where(TagQueryWithDays tagQueryWithDays)
    {
        return Where(x => tagQueryWithDays.TestPlace(x));
    }

    public Places Where(System.Func<Place, bool> predicate)
    {
        return new(
            _playerPlaces,
            _places.Where(predicate));
    }

    public Place FindNearestPlace()
    {
        return FindNearestPlace(_playerPlaces.PoI.XYZ);
    }

    public Place FindNearestPlace(
        Vec3d position)
    {
        return FindNearestPlaceOrDefault(position) ?? throw new UnreachableException("No places to find nearest from.");
    }

    public Place? FindNearestPlaceOrDefault(
        Vec3d position)
    {
        if (_places.Count == 0)
        {
            return null;
        }

        return _places.MinBy(x => position.SquareDistanceTo(x.XYZ))!;
    }

    public void Update(
        TagQuery tagQuery,
        Vec3d position,
        bool allowRemove,
        bool allowChange,
        bool allowAdd,
        out int numberOfRemovedPlaces,
        out int numberOfChangedPlaces,
        out int numberOfAddedPlaces)
    {
        TagQueryWithDays tagQueryWithDays = tagQuery.WithDays(_playerPlaces.PoI.Calendar);

        Update(
            tagQueryWithDays,
            position,
            allowRemove,
            allowChange,
            allowAdd,
            out numberOfRemovedPlaces,
            out numberOfChangedPlaces,
            out numberOfAddedPlaces);
    }

    public void Update(
        TagQueryWithDays tagQueryWithDays,
        Vec3d position,
        bool allowRemove,
        bool allowChange,
        bool allowAdd,
        out int numberOfRemovedPlaces,
        out int numberOfChangedPlaces,
        out int numberOfAddedPlaces)
    {
        numberOfRemovedPlaces = 0;
        numberOfChangedPlaces = 0;
        numberOfAddedPlaces = 0;

        if (Count == 0)
        {
            if (!allowAdd)
            {
                return;
            }

            Place place = new()
            {
                XYZ = position,
                Tags = [],
            };

            tagQueryWithDays.UpdatePlace(place, false, out bool _);

            if (place.Tags.Count > 0)
            {
                numberOfAddedPlaces += 1;
                AddToPlayerPlaces(place);
            }

            return;
        }

        if (!allowRemove && !allowChange)
        {
            return;
        }

        foreach (Place place in _places)
        {
            tagQueryWithDays.UpdatePlace(place, allowRemove, out bool tagsChanged);

            if (tagsChanged)
            {
                if (place.Tags.Count == 0)
                {
                    numberOfRemovedPlaces += 1;
                    RemoveFromPlayerPlaces(place);
                }
                else
                {
                    numberOfChangedPlaces += 1;
                }
            }
        }
    }

    public void AddToPlayerPlaces(Place place)
    {
        _playerPlaces.Add(place);
    }

    public void RemoveFromPlayerPlaces(Place place)
    {
        _playerPlaces.Remove(place);
    }

    public IEnumerator<Place> GetEnumerator()
    {
        return _places.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _places.GetEnumerator();
    }
}
