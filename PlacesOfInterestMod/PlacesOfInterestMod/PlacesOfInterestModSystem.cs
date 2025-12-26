using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
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
                SearchPlacesQuery searchPlacesQuery = SearchPlacesQuery.Parse(packet.TagQueriesText, this.Today());

                LoadPlaces(fromPlayer, out List<Place> places);

                searchPlacesQuery.SearchPlaces(places, out List<Place> matchingPlaces);

                _serverNetworkChannel.SendPacket(
                    new LoadedPlacesPacket()
                    {
                        Places = matchingPlaces
                            .Select(x => (ProtoPlace)x)
                            .ToList(),
                    },
                    fromPlayer);
            });

        _serverNetworkChannel.SetMessageHandler(
            (IServerPlayer fromPlayer, SavePlacesPacket packet) =>
            {
                List<Place> newPlaces = (packet.Places ?? [])
                    .SelectNonNulls(x => (Place?)x)
                    .ToList();
                ILookup<Vec3i, Place> newPlacesByRoughPlace = newPlaces
                    .ToLookup(x => x.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset));

                int day = this.Today();
                LoadPlaces(fromPlayer, out List<Place> places);

                foreach (IGrouping<Vec3i, Place> newPlaces2 in newPlacesByRoughPlace)
                {
                    List<Tag> newTags = newPlaces2
                        .SelectMany(x => x.Tags)
                        .Where(x => x.EndDay == 0 || x.EndDay >= day)
                        .DistinctBy(x => x.Name)
                        .ToList();

                    FindPlacesByRoughPlace(
                        places,
                        newPlaces2.Key,
                        out List<Place> placesCloseToPlayer);

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
                        AddOrUpdateOrRemovePlacesQuery addOrUpdateOrRemovePlacesQuery = AddOrUpdateOrRemovePlacesQuery.CreateForUpdate(
                            tagGroup.Names,
                            [],
                            packet.ExistingPlaceAction,
                            day,
                            tagGroup.StartDay,
                            tagGroup.EndDay);

                        addOrUpdateOrRemovePlacesQuery.AddOrUpdateOrRemovePlaces(
                            places,
                            placesCloseToPlayer,
                            newPlaces2.First().XYZ,
                            out int _,
                            out int _,
                            out int _);
                    }

                    if (packet.ExistingPlaceAction == ExistingPlaceAction.Replace)
                    {
                        List<TagName> tagNamesToRemove = oldTags
                            .Where(x => !newTags.Any(y => y.Name == x.Name))
                            .Select(x => x.Name)
                            .ToList();

                        AddOrUpdateOrRemovePlacesQuery addOrUpdateOrRemovePlacesQuery = AddOrUpdateOrRemovePlacesQuery.CreateForUpdate(
                            [],
                            tagNamesToRemove,
                            ExistingPlaceAction.Update,
                            day,
                            0,
                            0);

                        addOrUpdateOrRemovePlacesQuery.AddOrUpdateOrRemovePlaces(
                            places,
                            placesCloseToPlayer,
                            newPlaces2.First().XYZ,
                            out int _,
                            out int _,
                            out int _);
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

                    AddOrUpdateOrRemovePlacesQuery addOrUpdateOrRemovePlacesQuery = AddOrUpdateOrRemovePlacesQuery.Parse(
                        args.LastArg?.ToString() ?? "",
                        this.Today());

                    LoadPlaces(args.Caller.Player, out List<Place> places);

                    FindPlacesByRoughPlace(
                        places,
                        playerPosition.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset),
                        out List<Place> placesCloseToPlayer);

                    if (placesCloseToPlayer is [] && !addOrUpdateOrRemovePlacesQuery.HasTagNamesToInclude())
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:interestingCommandResultNothingToAdd"));
                    }

                    addOrUpdateOrRemovePlacesQuery.AddOrUpdateOrRemovePlaces(
                        places,
                        placesCloseToPlayer,
                        playerPosition,
                        out int _,
                        out int _,
                        out int _);

                    SavePlaces(args.Caller.Player, places);

                    if (placesCloseToPlayer is [])
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:interestingCommandResult",
                                FormTagsText(addOrUpdateOrRemovePlacesQuery.TagNamesToInclude, [], [], [])));
                    }
                    else
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:interestingCommandResultUpdated",
                                FormTagsText(
                                    addOrUpdateOrRemovePlacesQuery.TagNamesToInclude,
                                    [],
                                    addOrUpdateOrRemovePlacesQuery.TagNamesToExclude,
                                    addOrUpdateOrRemovePlacesQuery.TagPatternsToExclude)));
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

                    SearchPlacesQuery searchPlacesQuery = SearchPlacesQuery.Parse(args.LastArg?.ToString() ?? "", this.Today());

                    LoadPlaces(args.Caller.Player, out List<Place> places);

                    searchPlacesQuery.SearchPlaces(places, out List<Place> matchingPlaces);

                    if (matchingPlaces.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:noMatchingPlacesFound",
                                FormTagsText(
                                    searchPlacesQuery.TagNamesToInclude,
                                    searchPlacesQuery.TagPatternsToInclude,
                                    searchPlacesQuery.TagNamesToExclude,
                                    searchPlacesQuery.TagPatternsToExclude)));
                    }

                    FindNearestPlace(
                        matchingPlaces,
                        playerPosition,
                        out Place nearestPlace);

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:foundNearestPlace",
                            FormTagsText(nearestPlace.CalculateActiveTagNames(day), [], [], []),
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

                    LoadPlaces(args.Caller.Player, out List<Place> places);

                    FindPlacesInRadius(
                        places,
                        playerPosition,
                        searchRadius,
                        out List<Place> placesInRadius);

                    HashSet<TagName> uniqueTags = placesInRadius
                        .SelectMany(x => x.CalculateActiveTagNames(day))
                        .ToHashSet();

                    if (uniqueTags.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:noInterestingTagsFound"));
                    }

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:whatsSoInterestingResult",
                            FormTagsText(uniqueTags, [], [], [])));
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

                    SearchAndUpdateOrRemovePlacesQuery searchAndUpdateOrRemovePlacesQuery = SearchAndUpdateOrRemovePlacesQuery.Parse(
                        args.LastArg?.ToString() ?? "",
                        this.Today());

                    LoadPlaces(args.Caller.Player, out List<Place> places);

                    FindPlacesInRadius(
                        places,
                        playerPosition,
                        searchRadius,
                        out List<Place> placesCloseToPlayer);

                    if (placesCloseToPlayer.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:editInterestingPlacesResultNoPlacesFound",
                                searchRadius,
                                FormTagsText(
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagNamesToInclude,
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagPatternsToInclude,
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagNamesToExclude,
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagPatternsToExclude)));
                    }

                    searchAndUpdateOrRemovePlacesQuery.SearchAndUpdatePlaces(
                        places,
                        placesCloseToPlayer,
                        out int numberOfFoundPlaces,
                        out int _,
                        out int _);

                    if (numberOfFoundPlaces == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:editInterestingPlacesResultNoPlacesFound",
                                searchRadius,
                                FormTagsText(
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagNamesToInclude,
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagPatternsToInclude,
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagNamesToExclude,
                                    searchAndUpdateOrRemovePlacesQuery.SearchTagPatternsToExclude)));
                    }

                    SavePlaces(args.Caller.Player, places);

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:editInterestingPlacesResultUpdatedPlaces",
                            numberOfFoundPlaces,
                            FormTagsText(
                                searchAndUpdateOrRemovePlacesQuery.UpdateTagNamesToInclude,
                                searchAndUpdateOrRemovePlacesQuery.UpdateTagPatternsToInclude,
                                searchAndUpdateOrRemovePlacesQuery.UpdateTagNamesToExclude,
                                searchAndUpdateOrRemovePlacesQuery.UpdateTagPatternsToExclude)));
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
                List<SerializablePlace> places = (packet.Places ?? [])
                    .SelectNonNulls(x => (Place?)x)
                    .Select(x => (SerializablePlace)x)
                    .ToList();

                _clientApi.Forms.SetClipboardText(
                    JsonSerializer.Serialize(places));

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

                    List<SerializablePlace>? serializablePlaces;
                    try
                    {
                        serializablePlaces = JsonSerializer.Deserialize<List<SerializablePlace>>(clipboardText);
                    }
                    catch (Exception)
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:pasteInterestingPlacesResultInvalidClipboard"));
                    }

                    serializablePlaces ??= [];

                    List<ProtoPlace> newPlaces = serializablePlaces
                        .SelectNonNulls(x => (Place?)x)
                        .Select(x => (ProtoPlace)x)
                        .ToList();

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
        List<Place> places,
        Vec3d position,
        out Place nearestPlace)
    {
        if (places.Count == 0)
        {
            throw new UnreachableException("No places to find nearest from.");
        }

        nearestPlace = places.MinBy(x => position.SquareDistanceTo(x.XYZ))!;
    }

    private static string FormTagsText(
        IEnumerable<TagName> includedTagNames,
        IEnumerable<TagPattern> includedTagPatterns,
        IEnumerable<TagName> excludedTagNames,
        IEnumerable<TagPattern> excludedTagPatterns)
    {
#pragma warning disable S3220 // Method calls should not resolve ambiguously to overloads with "params"
        return string.Join(
            " ",
            [
                .. includedTagNames.OrderBy(x => x.Value.ToLowerInvariant()),
                .. includedTagPatterns.OrderBy(x => x.ToString().ToLowerInvariant()),
                .. excludedTagNames.OrderBy(x => x.Value.ToLowerInvariant()).Select(x => "-" + x),
                .. excludedTagPatterns.OrderBy(x => x.ToString().ToLowerInvariant()).Select(x => "-" + x),
            ]);
#pragma warning restore S3220 // Method calls should not resolve ambiguously to overloads with "params"
    }

    private static void FindPlacesByRoughPlace(
        List<Place> places,
        Vec3i roughPlayerPlacePosition,
        out List<Place> matchingPlaces)
    {
        matchingPlaces = places
            .Where(x => x.XYZ.ToRoughPlace(_roughPlaceResolution, _roughPlaceOffset) == roughPlayerPlacePosition)
            .ToList();
    }

    private static void FindPlacesInRadius(
        List<Place> places,
        Vec3d finePlayerPlacePosition,
        double searchRadius,
        out List<Place> matchingPlaces)
    {
        matchingPlaces = places
            .Where(x => x.XYZ.ToXZ().DistanceTo(finePlayerPlacePosition.ToXZ()) <= searchRadius)
            .ToList();
    }

    private static void SavePlaces(
    IPlayer player,
    List<Place> places)
    {
        List<ProtoPlace> protoPlaces = places
            .Select(x => (ProtoPlace)x)
            .ToList();

        player.WorldData.SetModdata(_placesOfInterestModDataKey, SerializerUtil.Serialize(protoPlaces));
    }

    private static void RemoveAllPlaces(IPlayer player)
    {
        player.WorldData.RemoveModdata(_placesOfInterestModDataKey);
    }

    private static void LoadPlaces(
        IPlayer player,
        out List<Place> places)
    {
        try
        {
            places = SerializerUtil
                .Deserialize<List<ProtoPlace>>(
                    player.WorldData.GetModdata(_placesOfInterestModDataKey),
                    [])
                .SelectNonNulls(x => (Place?)x)
                .ToList();
        }
        catch (ProtoException)
        {
            places = SerializerUtil
                .Deserialize<List<OldProtoPlace>>(
                    player.WorldData.GetModdata(_placesOfInterestModDataKey),
                    [])
                .SelectNonNulls(x => (Place?)x)
                .ToList();
        }
    }
}
