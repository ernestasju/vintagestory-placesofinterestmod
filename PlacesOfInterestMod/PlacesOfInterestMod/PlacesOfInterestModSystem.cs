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
                int day = this.Today();

                SearchPlacesQuery searchPlacesQuery = SearchPlacesQuery.Parse(packet.TagQueriesText, day);

                PlayerPlaces places = PlayerPlaces.Load(fromPlayer, day);

                List<Place> matchingPlaces = searchPlacesQuery.SearchPlaces(places.All).ToList();

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
                List<Place> newPlaces = (packet.Places ?? []).SelectNonNulls(x => (Place?)x).ToList();

                PlayerPlaces places = PlayerPlaces.Load(fromPlayer, this.Today());
                places.Import(newPlaces, packet.ExistingPlaceAction);

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
                    PlayerPlaces
                        .Load(args.Caller.Player, this.Today())
                        .Clear();

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
                    int day = this.Today();

                    AddOrUpdateOrRemovePlacesQuery addOrUpdateOrRemovePlacesQuery = AddOrUpdateOrRemovePlacesQuery.Parse(
                        args.LastArg?.ToString() ?? "",
                        day);

                    PlayerPlaces places = PlayerPlaces.Load(args.Caller.Player, day);

                    Places placesCloseToPlayer = places.All.AtPlayerPosition();

                    if (placesCloseToPlayer.Count == 0 && !addOrUpdateOrRemovePlacesQuery.HasTagNamesToInclude())
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:interestingCommandResultNothingToAdd"));
                    }

                    addOrUpdateOrRemovePlacesQuery.AddOrUpdateOrRemovePlaces(
                        placesCloseToPlayer,
                        args.Caller.Pos,
                        out int _,
                        out int _,
                        out int _);

                    places.Save();

                    if (placesCloseToPlayer.Count == 0)
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

                    SearchPlacesQuery searchPlacesQuery = SearchPlacesQuery.Parse(args.LastArg?.ToString() ?? "", day);

                    PlayerPlaces places = PlayerPlaces.Load(args.Caller.Player, day);

                    Places matchingPlaces = searchPlacesQuery.SearchPlaces(places.All);

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

                    Place nearestPlace = matchingPlaces.FindNearestPlace(playerPosition);

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

                    int day = this.Today();

                    PlayerPlaces playerPlaces = PlayerPlaces.Load(args.Caller.Player, day);

                    Places placesInRadius = playerPlaces.All.AroundPlayer(searchRadius);

                    HashSet<TagName> uniqueTags = placesInRadius.ActiveTags.ToHashSet();

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

                    SearchAndUpdateOrRemovePlacesQuery searchAndUpdateOrRemovePlacesQuery = SearchAndUpdateOrRemovePlacesQuery.Parse(
                        args.LastArg?.ToString() ?? "",
                        this.Today());

                    PlayerPlaces places = PlayerPlaces.Load(args.Caller.Player, this.Today());

                    Places placesCloseToPlayer = places.All.AroundPlayer(searchRadius);

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
                        placesCloseToPlayer,
                        out int numberOfFoundPlaces,
                        out int _,
                        out int _);

                    places.Save();

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
        _serverApi.ChatCommands.Create()
            .WithName("today")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription("Shows current day")
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    return TextCommandResult.Success(Today().ToString());
                });
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

    private static string FormTagsText(
        IEnumerable<TagName> includedTagNames,
        IEnumerable<TagPattern> includedTagPatterns,
        IEnumerable<TagName> excludedTagNames,
        IEnumerable<TagPattern> excludedTagPatterns)
    {
        string[] tags =
            [
                .. includedTagNames.OrderBy(x => x.Value.ToLowerInvariant()).Select(x => x.ToString()),
                .. includedTagPatterns.OrderBy(x => x.ToString().ToLowerInvariant()).Select(x => x.ToString()),
                .. excludedTagNames.OrderBy(x => x.Value.ToLowerInvariant()).Select(x => "-" + x),
                .. excludedTagPatterns.OrderBy(x => x.ToString().ToLowerInvariant()).Select(x => "-" + x),
            ];

        return string.Join(" ", tags);
    }
}
