using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                        out string[] excludedTags);

                    LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesByRoughPlace(
                        places,
                        playerPosition.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset),
                        out List<PlaceOfInterest> placesCloseToPlayer);

                    UpdatePlacesCloseToPlayer(
                        placesCloseToPlayer,
                        places,
                        playerPosition,
                        includedTags,
                        excludedTags);

                    SavePlaces(args.Caller.Player, places);

                    return TextCommandResult.Success(
                        Lang.Get("places-of-interest-mod:interestingCommandResult", FormTagsText(includedTags, excludedTags)));
                });

            _serverApi.ChatCommands.Create()
                .WithName("findInterestingPlace")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription(Lang.Get("places-of-interest-mod:findInterestingPlaceCommandDescription"))
                .WithArgs(
                    _serverApi.ChatCommands.Parsers.OptionalAll("tags"))
                .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
                {
                    Vec3d playerPosition = args.Caller.Pos;

                    ParseTags(
                        args,
                        out string[] includedTags,
                        out string[] excludedTags);

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
                            FormTagsText(nearestPlace.Tags, []),
                            playerPosition.ToXZ().DistanceTo(nearestPlace.XYZ.ToXZ()),
                            nearestPlace.XYZ.Y - playerPosition.Y));
                });

            _serverApi.ChatCommands.Create()
                .WithName("whatsSoInteresting")
                .RequiresPlayer()
                .RequiresPrivilege(Privilege.chat)
                .WithDescription(Lang.Get("places-of-interest-mod:whatsSoInterestingCommandDescription"))
                .WithArgs(
                    _serverApi.ChatCommands.Parsers.OptionalInt("radius", 16))
                .HandleWith(TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];

                    if (searchRadius <= 0)
                    {
                        searchRadius = 16;
                    }

                    Vec3d playerPosition = args.Caller.Pos;

                    LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesInRadius(
                        places,
                        playerPosition,
                        searchRadius,
                        out List<PlaceOfInterest> placesInRadius);

                    HashSet<string> uniqueTags = placesInRadius.SelectMany(x => x.Tags).ToHashSet();

                    if (uniqueTags.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:noInterestingTagsFound"));
                    }

                    return TextCommandResult.Success(
                        Lang.Get("places-of-interest-mod:whatsSoInterestingResult", FormTagsText(uniqueTags, [])));
                });
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

        private static void FindPlacesByTags(
            List<PlaceOfInterest> places,
            string[] includedTags,
            string[] excludedTags,
            out List<PlaceOfInterest> placesByTags)
        {
            placesByTags = places.Where(p => p.MatchesTags(includedTags, excludedTags)).ToList();
        }

        private static void UpdatePlacesCloseToPlayer(
            List<PlaceOfInterest> placesCloseToPlayer,
            List<PlaceOfInterest> allPlaces,
            Vec3d playerPosition,
            string[] tagsToAdd,
            string[] tagsToRemove)
        {
            if (placesCloseToPlayer.Count == 0)
            {
                allPlaces.Add(new PlaceOfInterest()
                {
                    XYZ = playerPosition,
                    Tags = [.. tagsToAdd],
                });
            }
            else
            {
                foreach (PlaceOfInterest place in placesCloseToPlayer)
                {
                    foreach (string tag in tagsToAdd)
                    {
                        place.Tags.Add(tag);
                    }
                    foreach (string tag in tagsToRemove)
                    {
                        place.Tags.Remove(tag);
                    }
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

        private static void LoadPlaces(
            IPlayer player,
            out List<PlaceOfInterest> places)
        {
            places = SerializerUtil.Deserialize<List<PlaceOfInterest>>(
                player.WorldData.GetModdata(_placesOfInterestModDataKey), []);
        }

        private static void ParseTags(
            TextCommandCallingArgs args,
            out string[] includedTags,
            out string[] excludedTags)
        {
            string tagsQueriesText = args.LastArg?.ToString() ?? "";
            string[] tagQueries = tagsQueriesText.Split(" ").Where(x => x != "").Select(x => x.ToLower()).ToArray() ?? [];
            includedTags = tagQueries.Where(t => !t.StartsWith("-")).Select(x => x).ToArray();
            excludedTags = tagQueries.Where(t => t.StartsWith("-")).Select(t => t.Substring(1)).ToArray();
        }
    }
}
