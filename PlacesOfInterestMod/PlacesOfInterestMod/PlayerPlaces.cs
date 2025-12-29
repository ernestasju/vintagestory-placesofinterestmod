using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public sealed class PlayerPlaces
{
    public const int RoughPlaceResolution = 8;

    private const string _placesOfInterestModDataKey = "places-of-interest-mod:placesOfInterest";
    private const int _roughPlaceOffset = 4;

    private readonly PlayerPlacesOfInterest _poi;
    private readonly List<Place> _places;

    private PlayerPlaces(PlayerPlacesOfInterest poi, IEnumerable<Place> places)
    {
        _poi = poi;
        _places = places.ToList();
    }

    public PlayerPlacesOfInterest PoI => _poi;

    public Places All => new(this, _places);

    public static Vec3i ToRoughPlace(Vec3d position)
    {
        return position.ToRoughPlace(RoughPlaceResolution, _roughPlaceOffset);
    }

    public static PlayerPlaces Load(PlayerPlacesOfInterest poi)
    {
        IEnumerable<Place> places;
        try
        {
            places = poi.Player
                .LoadModData<List<ProtoPlace>>(_placesOfInterestModDataKey, [])
                .SelectNonNulls(x => (Place?)x);
        }
        catch (ProtoException)
        {
            places = poi.Player
                .LoadModData<List<OldProtoPlace>>(_placesOfInterestModDataKey, [])
                .SelectNonNulls(x => (Place?)x);
        }

        return new(poi, places);
    }

    public void Clear()
    {
        _poi.Player.RemoveModData(_placesOfInterestModDataKey);

        _places.Clear();
    }

    public void Save()
    {
        List<ProtoPlace> protoPlaces = _places
            .Select(x => (ProtoPlace)x)
            .ToList();

        _poi.Player.SaveModData(_placesOfInterestModDataKey, protoPlaces);
    }

    public void Add(Place place)
    {
        _places.Add(place);
    }

    public void Remove(Place place)
    {
        _places.Remove(place);
    }

    public void Import(IEnumerable<Place> places, ExistingPlaceAction existingPlaceAction)
    {
        List<Place> newPlaces = places.ToList();

        ILookup<Vec3i, Place> newPlacesByRoughPlace = newPlaces
            .ToLookup(x => x.XYZ.ToRoughPlace(RoughPlaceResolution, _roughPlaceOffset));

        foreach (IGrouping<Vec3i, Place> newPlaces2 in newPlacesByRoughPlace)
        {
            List<Tag> newTags = newPlaces2
                .SelectMany(x => x.Tags)
                .Where(x => x.EndDay == 0 || x.EndDay >= _poi.Calendar.Today)
                .DistinctBy(x => x.Name)
                .ToList();

            Places placesCloseToPlayer = All.AtRoughPlace(newPlaces2.Key);

            List<Tag> oldTags = placesCloseToPlayer.Tags.ToList();

            if (oldTags.Count > 0 && existingPlaceAction == ExistingPlaceAction.Skip)
            {
                continue;
            }

            List<TagGroup> tagGroupsToAdd = newTags
                .GroupBy(x => new { x.StartDay, x.EndDay })
                .Select(x => new TagGroup()
                {
                    Names = x.Select(x => x.Name).ToArray(),
                    StartDay = x.Key.StartDay,
                    EndDay = x.Key.EndDay,
                })
                .ToList();

            foreach (TagGroup tagGroup in tagGroupsToAdd)
            {
                TagQueryWithDays tagQueryWithDays = TagQueryWithDays.CreateForUpdate(
                    tagGroup.Names,
                    [],
                    existingPlaceAction == ExistingPlaceAction.Replace,
                    _poi.Calendar.Today,
                    tagGroup.StartDay,
                    tagGroup.EndDay);

                placesCloseToPlayer.Update(
                    tagQueryWithDays,
                    newPlaces2.First().XYZ,
                    allowRemove: existingPlaceAction == ExistingPlaceAction.Replace,
                    allowChange: existingPlaceAction != ExistingPlaceAction.Skip,
                    allowAdd: true,
                    out int _,
                    out int _,
                    out int _);
            }

            if (existingPlaceAction == ExistingPlaceAction.Replace)
            {
                List<TagName> tagNamesToRemove = oldTags
                    .Where(x => !newTags.Any(y => y.Name == x.Name))
                    .Select(x => x.Name)
                    .ToList();

                TagQueryWithDays tagQueryWithDays = TagQueryWithDays.CreateForUpdate(
                    [],
                    tagNamesToRemove,
                    false,
                    _poi.Calendar.Today,
                    0,
                    0);

                placesCloseToPlayer.Update(
                    tagQueryWithDays,
                    newPlaces2.First().XYZ,
                    allowRemove: true,
                    allowChange: true,
                    allowAdd: true,
                    out int _,
                    out int _,
                    out int _);
            }
        }

        Save();
    }

    public double CalculateHorizontalDistance(Place place)
    {
        return _poi.XZ.DistanceTo(place.XYZ.ToXZ());
    }

    public double CalculateVerticalDistance(Place place)
    {
        return place.XYZ.Y - _poi.XYZ.Y;
    }
}
