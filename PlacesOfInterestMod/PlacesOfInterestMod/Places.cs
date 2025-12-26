using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class Places : IEnumerable<Place>
{
    private readonly PlayerPlaces _playerPlaces;
    private readonly IPlayer _player;
    private readonly int _day;
    private readonly int _roughPlaceResolution;
    private readonly int _roughPlaceOffset;
    private readonly List<Place> _places;

    public Places(
        PlayerPlaces playerPlaces,
        IPlayer player,
        int day,
        int roughPlaceResolution,
        int roughPlaceOffset,
        IEnumerable<Place> places)
    {
        _playerPlaces = playerPlaces;
        _player = player;
        _day = day;
        _roughPlaceResolution = roughPlaceResolution;
        _roughPlaceOffset = roughPlaceOffset;
        _places = places.ToList();
    }

    public int Count => _places.Count;

    public IEnumerable<Tag> Tags =>
        _places
            .SelectMany(x => x.Tags)
            .Where(x => x.EndDay == 0 || x.EndDay >= _day)
            .DistinctBy(x => x.Name);

    public IEnumerable<TagName> ActiveTags =>
        _places
            .SelectMany(x => x.CalculateActiveTagNames(_day));

    public Places AtPlayerPosition()
    {
        return AtRoughPlace(_player.Entity.Pos.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset));
    }

    public Places AtRoughPlace(Vec3i roughPlace)
    {
        return new(
            _playerPlaces,
            _player,
            _day,
            _roughPlaceResolution,
            _roughPlaceOffset,
            _places.Where(x => x.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset) == roughPlace));
    }

    public Places AroundPlayer(double radius)
    {
        Vec2d finePlayerPosition = _player.Entity.Pos.XYZ.ToXZ();

        return new(
            _playerPlaces,
            _player,
            _day,
            _roughPlaceResolution,
            _roughPlaceOffset,
            _places.Where(x => x.XYZ.ToXZ().DistanceTo(finePlayerPosition) <= radius));
    }

    public Places WithTags(TagQuery tagQuery)
    {
        return new(
            _playerPlaces,
            _player,
            _day,
            _roughPlaceResolution,
            _roughPlaceOffset,
            _places.Where(x => tagQuery.TestPlace(x)));
    }

    public Places Where(System.Func<Place, bool> predicate)
    {
        return new(
            _playerPlaces,
            _player,
            _day,
            _roughPlaceResolution,
            _roughPlaceOffset,
            _places.Where(predicate));
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
