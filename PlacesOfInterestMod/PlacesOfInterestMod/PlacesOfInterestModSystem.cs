﻿using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PlacesOfInterestMod
{
    public class PlacesOfInterestModSystem : ModSystem
    {
        private const string _placesOfInterestModDataKey = "places-of-interest-mod:placesOfInterest";
        private const int _roughPlaceResolution = 8;
        private const int _roughPlaceOffset = 4;

        private ICoreServerAPI? _serverApi;

        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Notification("Hello from Places of Interest mod: " + api.Side);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            _serverApi = api;

            Mod.Logger.Notification("Hello from Places of Interest server side: " + Lang.Get("places-of-interest-mod:hello"));

            RegisterChatCommands();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from Places of Interest client side: " + Lang.Get("places-of-interest-mod:hello"));
        }

        private void RegisterChatCommands()
        {
            ArgumentNullException.ThrowIfNull(_serverApi);

            _ = _serverApi.ChatCommands.Create()
                .WithName("clearInterestingPlaces")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription(Lang.Get("places-of-interest-mod:clearInterestingPlacesCommandDescription"))
                .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
                {
                    RemoveAllPlaces(args.Caller.Player);

                    return TextCommandResult.Success(Lang.Get("places-of-interest-mod:clearedInterestingPlaces"));
                });

            _ = _serverApi.ChatCommands.Create()
                .WithName("interesting")
                .WithAlias("tag")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription(Lang.Get("places-of-interest-mod:interestingCommandDescription"))
                .WithExamples(
                    Lang.Get("places-of-interest-mod:interestingCommandExample1"),
                    Lang.Get("places-of-interest-mod:interestingCommandExample2"))
                .WithArgs(
                    _serverApi.ChatCommands.Parsers.OptionalAll("tags"))
                .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
                {
                    Vec3d playerPosition = args.Caller.Pos;

                    ParseTags(
                        args,
                        out string[] includedTags,
                        out string[] excludedTags,
                        out int? startDayOffset,
                        out int? endDayOffset);

                    LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesByRoughPlace(
                        places,
                        playerPosition.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset),
                        out List<PlaceOfInterest> placesCloseToPlayer);

                    if (placesCloseToPlayer is [] && includedTags is [])
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:interestingCommandResultNothingToAdd"));
                    }

                    UpdatePlacesCloseToPlayer(
                        placesCloseToPlayer,
                        places,
                        playerPosition,
                        includedTags,
                        excludedTags,
                        startDayOffset,
                        endDayOffset);

                    SavePlaces(args.Caller.Player, places);

                    if (placesCloseToPlayer is [])
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:interestingCommandResult", FormTagsText(includedTags, excludedTags)));
                    }
                    else
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:interestingCommandResultUpdated", FormTagsText(includedTags, excludedTags)));
                    }
                });

            _serverApi.ChatCommands.Create()
                .WithName("findInterestingPlace")
                .WithAlias("dist")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription(Lang.Get("places-of-interest-mod:findInterestingPlaceCommandDescription"))
                .WithArgs(
                    _serverApi.ChatCommands.Parsers.All("tags"))
                .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
                {
                    Vec3d playerPosition = args.Caller.Pos;
                    int day = Today();

                    ParseTags(
                        args,
                        out string[] includedTags,
                        out string[] excludedTags,
                        out int? _,
                        out int? _);

                    LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesByTags(
                        places,
                        includedTags,
                        excludedTags,
                        out List<PlaceOfInterest> matchingPlaces);

                    if (matchingPlaces.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:noMatchingPlacesFound", FormTagsText(includedTags, excludedTags)));
                    }

                    FindNearestPlace(
                        matchingPlaces,
                        playerPosition,
                        out PlaceOfInterest nearestPlace);

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:foundNearestPlace",
                            FormTagsText(nearestPlace.GetActiveTagNames(day), []),
                            (int)Math.Round(playerPosition.ToXZ().DistanceTo(nearestPlace.XYZ.ToXZ())),
                            (int)Math.Round(nearestPlace.XYZ.Y - playerPosition.Y)));
                });

            _serverApi.ChatCommands.Create()
                .WithName("whatsSoInteresting")
                .WithAlias("tags")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription(Lang.Get("places-of-interest-mod:whatsSoInterestingCommandDescription"))
                .WithArgs(
                    _serverApi.ChatCommands.Parsers.OptionalInt("radius", 100))
                .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];

                    if (searchRadius <= 0)
                    {
                        searchRadius = 16;
                    }
                    
                    Vec3d playerPosition = args.Caller.Pos;
                    int day = Today();

                    LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesInRadius(
                        places,
                        playerPosition,
                        searchRadius,
                        out List<PlaceOfInterest> placesInRadius);

                    HashSet<string> uniqueTags = placesInRadius.SelectMany(x => x.GetActiveTagNames(day)).ToHashSet();

                    if (uniqueTags.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:noInterestingTagsFound"));
                    }

                    return TextCommandResult.Success(
                        Lang.Get("places-of-interest-mod:whatsSoInterestingResult", FormTagsText(uniqueTags, [])));
                });

            //_serverApi.ChatCommands.Create()
            //    .WithName("today")
            //    .RequiresPlayer()
            //    .RequiresPrivilege(Privilege.chat)
            //    .WithDescription("Shows current day")
            //    .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
            //    {
            //        return TextCommandResult.Success(Today().ToString());
            //    });
        }

        private int Today()
        {
            ArgumentNullException.ThrowIfNull(_serverApi);

            return (int)_serverApi.World.Calendar.TotalDays;
        }

        private static void FindNearestPlace(
            List<PlaceOfInterest> places,
            Vec3d position,
            out PlaceOfInterest nearestPlace)
        {
            if (places.Count == 0)
            {
                throw new UnreachableException("No places to find nearest from.");
            }

            nearestPlace = places.MinBy(x => position.SquareDistanceTo(x.XYZ))!;
        }

        private static string FormTagsText(
            IEnumerable<string> includedTags,
            IEnumerable<string> excludedTags)
        {
            return string.Join(" ", [.. includedTags.Order(), .. excludedTags.Select(x => "-" + x).Order()]);
        }

        private void FindPlacesByTags(
            List<PlaceOfInterest> places,
            string[] includedTags,
            string[] excludedTags,
            out List<PlaceOfInterest> placesByTags)
        {
            int day = Today();

            placesByTags = places
                .Where(p =>
                    p.MatchesTags(
                        day,
                        includedTags,
                        excludedTags,
                        wildcardsInIncludedTags: true,
                        wildcardInExcludedTags: true))
                .ToList();
        }

        private void UpdatePlacesCloseToPlayer(
            List<PlaceOfInterest> placesCloseToPlayer,
            List<PlaceOfInterest> allPlaces,
            Vec3d playerPosition,
            string[] tagsToAdd,
            string[] tagsToRemove,
            int? startDayOffset,
            int? endDayOffset)
        {
            int day = Today();

            int startDay = 0;
            int endDay = 0;
            if (startDayOffset is int nonNullStartDayOffset && nonNullStartDayOffset > 0)
            {
                startDay = day + nonNullStartDayOffset;
            }
            if (endDayOffset is int nonNullEndDayOffset && nonNullEndDayOffset > 0)
            {
                endDay = day + nonNullEndDayOffset;
            }

            if (placesCloseToPlayer.Count == 0)
            {
                PlaceOfInterest place = new()
                {
                    XYZ = playerPosition,
                    Tags = [.. tagsToAdd.Select(x => new Tag()
                    {
                        Name = x,
                        StartDay = startDay,
                        EndDay = endDay,
                    })],
                };


                allPlaces.Add(place);
            }
            else
            {
                List<PlaceOfInterest> placesToRemove = [];

                foreach (PlaceOfInterest place in placesCloseToPlayer)
                {
                    place.UpdateTags(tagsToRemove, tagsToAdd, startDay, endDay);

                    if (place.Tags.Count == 0)
                    {
                        placesToRemove.Add(place);
                        continue;
                    }
                }

                foreach (PlaceOfInterest place in placesToRemove)
                {
                    allPlaces.Remove(place);
                }
            }
        }

        private static void SavePlaces(
            IPlayer player,
            List<PlaceOfInterest> places)
        {
            player.WorldData.SetModdata(_placesOfInterestModDataKey, SerializerUtil.Serialize(places));
        }

        private static void RemoveAllPlaces(IPlayer player)
        {
            player.WorldData.RemoveModdata(_placesOfInterestModDataKey);
        }

        private static void FindPlacesByRoughPlace(
            List<PlaceOfInterest> places,
            Vec3i roughPlayerPlacePosition,
            out List<PlaceOfInterest> matchingPlaces)
        {
            matchingPlaces = places.Where(p => p.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset) == roughPlayerPlacePosition).ToList();
        }

        private static void FindPlacesInRadius(
            List<PlaceOfInterest> places,
            Vec3d finePlayerPlacePosition,
            double searchRadius,
            out List<PlaceOfInterest> matchingPlaces)
        {
            matchingPlaces = places.Where(x => x.XYZ.ToXZ().DistanceTo(finePlayerPlacePosition.ToXZ()) <= searchRadius).ToList();
        }

        private void LoadPlaces(
            IPlayer player,
            out List<PlaceOfInterest> places)
        {
            int day = Today();

            try
            {
                places =
                    SerializerUtil.Deserialize<List<PlaceOfInterest>>(
                        player.WorldData.GetModdata(_placesOfInterestModDataKey), [])
                    .Where(x =>
                    {
                        if (x.Tags is null)
                        {
                            return false;
                        }

                        x.Tags.RemoveAll(x => string.IsNullOrEmpty(x.Name));

                        return x.Tags.Count > 0;
                    })
                    .ToList();
            }
            catch (ProtoException)
            {
                List<OldPlaceOfInterest> oldPlaceOfInterests =
                    SerializerUtil.Deserialize<List<OldPlaceOfInterest>>(
                        player.WorldData.GetModdata(_placesOfInterestModDataKey), [])
                    .Where(x =>
                    {
                        if (x.Tags is null)
                        {
                            return false;
                        }

                        x.Tags.RemoveWhere(x => string.IsNullOrEmpty(x));

                        return x.Tags.Count > 0;
                    })
                    .ToList();

                places = oldPlaceOfInterests
                    .Select(oldPlace => new PlaceOfInterest()
                    {
                        XYZ = oldPlace.XYZ,
                        Tags = oldPlace.Tags
                            .Where(x => !string.IsNullOrEmpty(x))
                            .Select(tagName => new Tag()
                            {
                                Name = tagName,
                                StartDay = 0,
                                EndDay = 0,
                            })
                            .ToList(),
                    })
                    .ToList();
            }

            foreach (PlaceOfInterest place in places)
            {
                place.Validate();
            }
        }

        private static void ParseTags(
            TextCommandCallingArgs args,
            out string[] includedTags,
            out string[] excludedTags,
            out int? startDayOffset,
            out int? endDayOffset)
        {
            startDayOffset = null;
            endDayOffset = null;

            string tagsQueriesText = args.LastArg?.ToString() ?? "";

            List<string> includedTags2 = [];
            List<string> excludedTags2 = [];
            foreach (string tagQueryText in tagsQueriesText.Split(" ").Where(x => x != "") ?? [])
            {
                Match match = Regex.Match(tagQueryText.ToLower(), @"^(?<Sign>[\+\-])?(?:(?<Number>[1-9]\d*)(?<Unit>[yqmwd])|(?<Tag>.*))$");
                if (!match.Success)
                {
                    continue;
                }

                string sign = match.Groups["Sign"].Value;

                if (match.Groups["Number"].Success && match.Groups["Unit"].Success)
                {
                    if (!int.TryParse(match.Groups["Number"].Value, out int number))
                    {
                        continue;
                    }

                    number *= match.Groups["Unit"].Value switch
                    {
                        "y" => 365,
                        "q" => 91,
                        "m" => 30,
                        "w" => 7,
                        "d" => 1,
                        // NOTE: Unreachable due to regex.
                        _ => 0,
                    };

                    if (sign is "+" or "-")
                    {
                        // NOTE: Saying I don't way to see this place for 7 days is equivalent to saying I want to see it in 7 days.
                        startDayOffset = number;
                        continue;
                    }                    

                    if (sign is "")
                    {
                        endDayOffset = number;
                        continue;
                    }
                }

                string tag = match.Groups["Tag"].Value;

                if (sign is "-")
                {
                    excludedTags2.Add(tag);
                }
                else
                {
                    includedTags2.Add(tag);
                }
            }

            includedTags = includedTags2.ToArray();
            excludedTags = excludedTags2.ToArray();
        }
    }
}
