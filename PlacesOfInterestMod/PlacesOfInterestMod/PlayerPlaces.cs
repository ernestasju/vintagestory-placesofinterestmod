using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace PlacesOfInterestMod;

public sealed class PlayerPlaces
{
    private const string _placesOfInterestModDataKey = "places-of-interest-mod:placesOfInterest";
    private const int _roughPlaceResolution = 8;
    private const int _roughPlaceOffset = 4;

    private readonly IPlayer _player;
    private readonly int _day;
    private readonly List<Place> _places;

    public PlayerPlaces(IPlayer player, int day, IEnumerable<Place> places)
    {
        _player = player;
        _day = day;
        _places = places.ToList();
    }

    public Places All => new(this, _player, _day, _roughPlaceResolution, _roughPlaceOffset, _places);

    public void Clear()
    {
        _player.WorldData.RemoveModdata(_placesOfInterestModDataKey);

        _places.Clear();
    }

    public void Save()
    {
        List<ProtoPlace> protoPlaces = _places
            .Select(x => (ProtoPlace)x)
            .ToList();

        _player.WorldData.SetModdata(_placesOfInterestModDataKey, SerializerUtil.Serialize(protoPlaces));
    }

    public void Add(Place place)
    {
        _places.Add(place);
    }

    public void Remove(Place place)
    {
        _places.Remove(place);
    }

    public static PlayerPlaces Load(IPlayer player, int day)
    {
        byte[] modData = player.WorldData.GetModdata(_placesOfInterestModDataKey);

        IEnumerable<Place> places;
        try
        {
            places = SerializerUtil
                .Deserialize<List<ProtoPlace>>(modData, [])
                .SelectNonNulls(x => (Place?)x);
        }
        catch (ProtoException)
        {
            places = SerializerUtil
                .Deserialize<List<OldProtoPlace>>(modData, [])
                .SelectNonNulls(x => (Place?)x);
        }

        return new(player, day, places);
    }

    public void Import(IEnumerable<Place> places, ExistingPlaceAction existingPlaceAction)
    {
        List<Place> newPlaces = places.ToList();

        ILookup<Vec3i, Place> newPlacesByRoughPlace = newPlaces
            .ToLookup(x => x.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset));

        foreach (IGrouping<Vec3i, Place> newPlaces2 in newPlacesByRoughPlace)
        {
            List<Tag> newTags = newPlaces2
                .SelectMany(x => x.Tags)
                .Where(x => x.EndDay == 0 || x.EndDay >= _day)
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
                AddOrUpdateOrRemovePlacesQuery addOrUpdateOrRemovePlacesQuery = AddOrUpdateOrRemovePlacesQuery.CreateForUpdate(
                    tagGroup.Names,
                    [],
                    existingPlaceAction,
                    _day,
                    tagGroup.StartDay,
                    tagGroup.EndDay);

                addOrUpdateOrRemovePlacesQuery.AddOrUpdateOrRemovePlaces(
                    placesCloseToPlayer,
                    newPlaces2.First().XYZ,
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

                AddOrUpdateOrRemovePlacesQuery addOrUpdateOrRemovePlacesQuery = AddOrUpdateOrRemovePlacesQuery.CreateForUpdate(
                    [],
                    tagNamesToRemove,
                    ExistingPlaceAction.Update,
                    _day,
                    0,
                    0);

                addOrUpdateOrRemovePlacesQuery.AddOrUpdateOrRemovePlaces(
                    placesCloseToPlayer,
                    newPlaces2.First().XYZ,
                    out int _,
                    out int _,
                    out int _);
            }
        }

        Save();
    }
}
