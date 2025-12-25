using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PlacesOfInterestMod;

public class PlacesOfInterestModSystem : ModSystem
{
    private const string _placesOfInterestModDataKey = "places-of-interest-mod:placesOfInterest";
    private const int _roughPlaceResolution = 8;
    private const int _roughPlaceOffset = 4;

    private const string _placeOfInterestNetworkChannelName = "places-of-interest-mod";

    private ICoreServerAPI? _serverApi;
    private ICoreClientAPI? _clientApi;

    private IServerNetworkChannel? _serverNetworkChannel;
    private IClientNetworkChannel? _clientNetworkChannel;

    public override void Start(ICoreAPI api)
    {
        Mod.Logger.Notification("Hello from Places of Interest mod: " + api.Side);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _serverApi = api;

        Mod.Logger.Notification("Hello from Places of Interest server side: " + Lang.Get("places-of-interest-mod:hello"));

        RegisterServerNetworkChannels();
        RegisterServerChatCommands();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;

        Mod.Logger.Notification("Hello from Places of Interest client side: " + Lang.Get("places-of-interest-mod:hello"));

        RegisterClientNetworkChannels();
        RegisterClientChatCommands();
    }

    private void RegisterServerNetworkChannels()
    {
        ArgumentNullException.ThrowIfNull(_serverApi);

        _serverNetworkChannel = _serverApi.Network.RegisterChannel(_placeOfInterestNetworkChannelName);
        _serverNetworkChannel.RegisterMessageType<LoadPlacesPacket>();
        _serverNetworkChannel.RegisterMessageType<LoadedPlacesPacket>();
        _serverNetworkChannel.RegisterMessageType<SavePlacesPacket>();
        _serverNetworkChannel.RegisterMessageType<SavedPlacesPacket>();

        _serverNetworkChannel.SetMessageHandler(
            (IServerPlayer fromPlayer, LoadPlacesPacket packet) =>
            {
                ParseTags(
                    packet.TagQueriesText,
                    out string[] includedTags,
                    out string[] excludedTags,
                    out int? _,
                    out int? _);

                this.LoadPlaces(
                    fromPlayer,
                    out List<PlaceOfInterest> places);

                this.FindPlacesByTags(
                    places,
                    includedTags,
                    excludedTags,
                    out List<PlaceOfInterest> matchingPlaces);

                _serverNetworkChannel.SendPacket(
                    new LoadedPlacesPacket()
                    {
                        Places = matchingPlaces,
                    },
                    fromPlayer);
            });

        _serverNetworkChannel.SetMessageHandler(
            (IServerPlayer fromPlayer, SavePlacesPacket packet) =>
            {
                List<PlaceOfInterest> newPlaces = packet.Places ?? [];
                ILookup<Vec3i, PlaceOfInterest> newPlacesByRoughPlace = newPlaces.ToLookup(x => x.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset));

                int day = this.Today();
                this.LoadPlaces(
                    fromPlayer,
                    out List<PlaceOfInterest> places);

                foreach (IGrouping<Vec3i, PlaceOfInterest> newPlaces2 in newPlacesByRoughPlace)
                {
                    List<Tag> newTags = newPlaces2
                        .SelectMany(x => x.Tags)
                        .Where(x => x.EndDay == 0 || x.EndDay >= day)
                        .DistinctBy(x => x.Name)
                        .ToList();

                    FindPlacesByRoughPlace(
                        places,
                        newPlaces2.Key,
                        out List<PlaceOfInterest> placesCloseToPlayer);

                    List<Tag> oldTags = placesCloseToPlayer
                        .SelectMany(x => x.Tags)
                        .Where(x => x.EndDay == 0 || x.EndDay >= day)
                        .DistinctBy(x => x.Name)
                        .ToList();

                    if (oldTags.Count > 0 && packet.ExistingPlaceAction == ExistingPlaceAction.Skip)
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
                        int? startDayOffset = tagGroup.StartDay > day ? tagGroup.StartDay - day : null;
                        int? endDayOffset = tagGroup.EndDay >= day ? tagGroup.EndDay - day : null;
                        UpdatePlacesCloseToPlayer(placesCloseToPlayer, places, newPlaces2.First().XYZ, tagGroup.Names, [], startDayOffset, endDayOffset);
                    }

                    if (packet.ExistingPlaceAction == ExistingPlaceAction.Replace)
                    {
                        List<string> tagsToRemove = oldTags
                            .Where(x => !newTags.Any(y => y.Name == x.Name))
                            .Select(x => x.Name)
                            .ToList();

                        foreach (string tagToRemove in tagsToRemove)
                        {
                            UpdatePlacesCloseToPlayer(placesCloseToPlayer, places, newPlaces2.First().XYZ, [], [tagToRemove], null, null);
                        }
                    }
                }

                SavePlaces(fromPlayer, places);

                _serverNetworkChannel.SendPacket(
                    new SavedPlacesPacket()
                    {
                        PlacesCount = newPlaces.Count,
                    },
                    fromPlayer);
            });
    }

    private void RegisterServerChatCommands()
    {
        ArgumentNullException.ThrowIfNull(_serverApi);

        _ = _serverApi.ChatCommands.Create()
            .WithName("clearInterestingPlaces")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get("places-of-interest-mod:clearInterestingPlacesCommandDescription"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    RemoveAllPlaces(args.Caller.Player);

                    return TextCommandResult.Success(
                        Lang.Get("places-of-interest-mod:clearedInterestingPlaces"));
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
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    Vec3d playerPosition = args.Caller.Pos;

                    ParseTags(
                        args.LastArg?.ToString() ?? "",
                        out string[] includedTags,
                        out string[] excludedTags,
                        out int? startDayOffset,
                        out int? endDayOffset);

                    this.LoadPlaces(
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

                    this.UpdatePlacesCloseToPlayer(
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
                            Lang.Get(
                                "places-of-interest-mod:interestingCommandResult",
                                FormTagsText(includedTags, excludedTags)));
                    }
                    else
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:interestingCommandResultUpdated",
                                FormTagsText(includedTags, excludedTags)));
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
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    Vec3d playerPosition = args.Caller.Pos;
                    int day = this.Today();

                    ParseTags(
                        args.LastArg?.ToString() ?? "",
                        out string[] includedTags,
                        out string[] excludedTags,
                        out int? _,
                        out int? _);

                    this.LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    this.FindPlacesByTags(
                        places,
                        includedTags,
                        excludedTags,
                        out List<PlaceOfInterest> matchingPlaces);

                    if (matchingPlaces.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:noMatchingPlacesFound",
                                FormTagsText(includedTags, excludedTags)));
                    }

                    FindNearestPlace(
                        matchingPlaces,
                        playerPosition,
                        out PlaceOfInterest nearestPlace);

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:foundNearestPlace",
                            FormTagsText(nearestPlace.CalculateActiveTagNames(day), []),
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
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];

                    if (searchRadius <= 0)
                    {
                        searchRadius = 16;
                    }

                    Vec3d playerPosition = args.Caller.Pos;
                    int day = this.Today();

                    this.LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesInRadius(
                        places,
                        playerPosition,
                        searchRadius,
                        out List<PlaceOfInterest> placesInRadius);

                    HashSet<string> uniqueTags = placesInRadius.SelectMany(x => x.CalculateActiveTagNames(day)).ToHashSet();

                    if (uniqueTags.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:noInterestingTagsFound"));
                    }

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:whatsSoInterestingResult",
                            FormTagsText(uniqueTags, [])));
                });

        _serverApi.ChatCommands.Create()
            .WithName("editInterestingPlaces")
            .WithAlias("editTags")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get("places-of-interest-mod:editInterestingPlacesCommandDescription"))
            .WithExamples(
                Lang.Get("places-of-interest-mod:editInterestingPlacesCommandExample1"),
                Lang.Get("places-of-interest-mod:editInterestingPlacesCommandExample2"),
                Lang.Get("places-of-interest-mod:editInterestingPlacesCommandExample3"))
            .WithArgs(
                _serverApi.ChatCommands.Parsers.OptionalInt("radius", 16),
                _serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];

                    if (searchRadius <= 0)
                    {
                        searchRadius = int.MaxValue;
                    }

                    Vec3d playerPosition = args.Caller.Pos;

                    string tagQueriesText = $" {args.LastArg?.ToString() ?? ""} ";
                    if (!tagQueriesText.Contains(" -> "))
                    {
                        tagQueriesText = " -> " + tagQueriesText.TrimStart();
                    }

                    if (tagQueriesText.Split(" -> ", 2) is not [string oldTags, string newTags])
                    {
                        throw new UnreachableException();
                    }

                    ParseTags(
                        oldTags,
                        out string[] oldIncludedTags,
                        out string[] oldExcludedTags,
                        out int? _,
                        out int? _);

                    ParseTags(
                        newTags,
                        out string[] newIncludedTags,
                        out string[] newExcludedTags,
                        out int? newStartDayOffset,
                        out int? newEndDayOffset);

                    this.LoadPlaces(
                        args.Caller.Player,
                        out List<PlaceOfInterest> places);

                    FindPlacesInRadius(
                        places,
                        playerPosition,
                        searchRadius,
                        out List<PlaceOfInterest> matchingPlaces);

                    if (matchingPlaces.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:editInterestingPlacesResultNoPlacesFound",
                                searchRadius,
                                FormTagsText(oldIncludedTags, oldExcludedTags)));
                    }

                    FindPlacesByTags(
                        matchingPlaces,
                        oldIncludedTags,
                        oldExcludedTags,
                        out List<PlaceOfInterest> placesCloseToPlayer);

                    if (placesCloseToPlayer.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:editInterestingPlacesResultNoPlacesFound",
                                searchRadius,
                                FormTagsText(oldIncludedTags, oldExcludedTags)));
                    }

                    this.UpdatePlacesCloseToPlayer(
                        placesCloseToPlayer,
                        places,
                        playerPosition,
                        newIncludedTags,
                        newExcludedTags,
                        newStartDayOffset,
                        newEndDayOffset);

                    SavePlaces(args.Caller.Player, places);

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:editInterestingPlacesResultUpdatedPlaces",
                            placesCloseToPlayer.Count,
                            FormTagsText(newIncludedTags, newExcludedTags)));
                });

        // NOTE: Commented for later.
        // _serverApi.ChatCommands.Create()
        //     .WithName("today")
        //     .RequiresPlayer()
        //     .RequiresPrivilege(Privilege.chat)
        //     .WithDescription("Shows current day")
        //     .HandleWith(
        //         TextCommandResult (TextCommandCallingArgs args) =>
        //         {
        //             return TextCommandResult.Success(Today().ToString());
        //         });
    }

    private void RegisterClientNetworkChannels()
    {
        ArgumentNullException.ThrowIfNull(_clientApi);

        _clientNetworkChannel = _clientApi.Network.RegisterChannel(_placeOfInterestNetworkChannelName);
        _clientNetworkChannel.RegisterMessageType<LoadPlacesPacket>();
        _clientNetworkChannel.RegisterMessageType<LoadedPlacesPacket>();
        _clientNetworkChannel.RegisterMessageType<SavePlacesPacket>();
        _clientNetworkChannel.RegisterMessageType<SavedPlacesPacket>();

        _clientNetworkChannel.SetMessageHandler(
            (LoadedPlacesPacket packet) =>
            {
                var places = packet.Places ?? [];

                _clientApi.Forms.SetClipboardText(
                    JsonSerializer.Serialize(
                        places.Select(x => (SerializablePlaceOfInterest)x)));

                _clientApi.TriggerChatMessage(Lang.Get(
                    "places-of-interest-mod:copyInterestingPlacesResult",
                    places.Count));
            });

        _clientNetworkChannel.SetMessageHandler(
            (SavedPlacesPacket packet) =>
            {
                _clientApi.TriggerChatMessage(Lang.Get(
                    "places-of-interest-mod:pasteInterestingPlacesResult",
                    packet.PlacesCount));
            });
    }

    private void RegisterClientChatCommands()
    {
        ArgumentNullException.ThrowIfNull(_clientApi);
        ArgumentNullException.ThrowIfNull(_clientNetworkChannel);

        _ = _clientApi.ChatCommands.Create()
            .WithName("copyInterestingPlaces")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get("places-of-interest-mod:copyInterestingPlacesCommandDescription"))
            .WithExamples(
                Lang.Get("places-of-interest-mod:copyInterestingPlacesCommandExample1"),
                Lang.Get("places-of-interest-mod:copyInterestingPlacesCommandExample2"))
            .WithArgs(
                _clientApi.ChatCommands.Parsers.OptionalInt("radius", 0),
                _clientApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];

                    if (searchRadius < 0)
                    {
                        searchRadius = 16;
                    }

                    string tagQueriesText = args.LastArg?.ToString() ?? "";

                    _clientNetworkChannel.SendPacket(new LoadPlacesPacket()
                    {
                        SearchRadius = searchRadius,
                        TagQueriesText = tagQueriesText,
                    });

                    return TextCommandResult.Success(
                        Lang.Get("places-of-interest-mod:copyInterestingPlacesResultDownloadInProgress"));
                });

        _ = _clientApi.ChatCommands.Create()
            .WithName("pasteInterestingPlaces")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get("places-of-interest-mod:pasteInterestingPlacesCommandDescription"))
            .WithExamples(
                Lang.Get("places-of-interest-mod:pasteInterestingPlacesCommandExample1"),
                Lang.Get("places-of-interest-mod:pasteInterestingPlacesCommandExample2"),
                Lang.Get("places-of-interest-mod:pasteInterestingPlacesCommandExample3"))
            .WithArgs(
                _clientApi.ChatCommands.Parsers.OptionalWordRange("existing place action", "update", "skip", "replace"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    ArgumentNullException.ThrowIfNull(_clientApi);
                    string? clipboardText = _clientApi.Forms.GetClipboardText() is string value && !string.IsNullOrWhiteSpace(value) ? value : null;

                    if (clipboardText is null)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:pasteInterestingPlacesResultNoClipboard"));
                    }

                    // NOTE: Arg is guaranteed to exist.
                    string? existingPlaceActionText = args[0] as string;

                    ExistingPlaceAction existingPlaceAction = existingPlaceActionText?.ToLower() switch
                    {
                        "skip" => ExistingPlaceAction.Skip,
                        "update" => ExistingPlaceAction.Update,
                        "replace" => ExistingPlaceAction.Replace,
                        _ => ExistingPlaceAction.Skip,
                    };

                    List<SerializablePlaceOfInterest>? serializablePlaces;
                    try
                    {
                        serializablePlaces = JsonSerializer.Deserialize<List<SerializablePlaceOfInterest>>(clipboardText);
                    }
                    catch (Exception)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:pasteInterestingPlacesResultInvalidClipboard"));
                    }

                    serializablePlaces ??= [];

                    List<PlaceOfInterest> newPlaces = serializablePlaces.Select(x => (PlaceOfInterest)x).ToList();

                    _clientNetworkChannel.SendPacket(
                        new SavePlacesPacket()
                        {
                            ExistingPlaceAction = existingPlaceAction,
                            Places = newPlaces,
                        });

                    return TextCommandResult.Success(
                        Lang.Get("places-of-interest-mod:pasteInterestingPlacesResultUploadInProgress"));
                });
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
#pragma warning disable S3220 // Method calls should not resolve ambiguously to overloads with "params"
        return string.Join(" ", [.. includedTags.Order(), .. excludedTags.Select(x => "-" + x).Order()]);
#pragma warning restore S3220 // Method calls should not resolve ambiguously to overloads with "params"
    }

    private void FindPlacesByTags(
        List<PlaceOfInterest> places,
        string[] includedTags,
        string[] excludedTags,
        out List<PlaceOfInterest> placesByTags)
    {
        placesByTags = places
            .Where(x =>
                x.MatchesTags(
                    this.Today(),
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
        int day = this.Today();

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
                Tags = tagsToAdd
                    .Select(x => new Tag()
                    {
                        Name = x,
                        StartDay = startDay,
                        EndDay = endDay,
                    })
                    .ToList(),
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
        matchingPlaces = places.Where(x => x.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset) == roughPlayerPlacePosition).ToList();
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
        try
        {
            places = SerializerUtil
                .Deserialize<List<PlaceOfInterest>>(
                    player.WorldData.GetModdata(_placesOfInterestModDataKey),
                    [])
                .Where(
                    x =>
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
            List<OldPlaceOfInterest> oldPlaceOfInterests = SerializerUtil
                .Deserialize<List<OldPlaceOfInterest>>(
                    player.WorldData.GetModdata(_placesOfInterestModDataKey),
                    [])
                .Where(
                    x =>
                    {
                        if (x.Tags is null)
                        {
                            return false;
                        }

                        x.Tags.RemoveWhere(x => string.IsNullOrEmpty(x));

                        return x.Tags.Count > 0;
                    })
                .ToList();

#pragma warning disable T0010 // Internal Styling Rule T0010
            places = oldPlaceOfInterests
                .Select(
                    oldPlace => new PlaceOfInterest()
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
#pragma warning restore T0010 // Internal Styling Rule T0010
        }

        foreach (PlaceOfInterest place in places)
        {
            place.Validate();
        }
    }

    private static void ParseTags(
        string tagQueriesText,
        out string[] includedTags,
        out string[] excludedTags,
        out int? startDayOffset,
        out int? endDayOffset)
    {
        startDayOffset = null;
        endDayOffset = null;

        List<string> includedTags2 = [];
        List<string> excludedTags2 = [];
        foreach (string tagQueryText in tagQueriesText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = Regex.Match(tagQueryText.ToLower(), @"^(?<Sign>[\+\-])?(?:(?<Number>[1-9]\d*)(?<Unit>[yqmwd])|(?<Tag>.*))$");
            if (!match.Success)
            {
                continue;
            }

            string sign = match.Groups["Sign"].Value;
            ArgumentNullException.ThrowIfNull(sign);

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

                if (sign == "")
                {
                    endDayOffset = number;
                    continue;
                }
            }

            string tag = match.Groups["Tag"].Value;

            if (sign == "-")
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
