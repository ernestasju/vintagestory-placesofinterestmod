using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

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
                PlayerPlacesOfInterest poi = new(fromPlayer);

                _serverNetworkChannel.SendPacket(
                    new LoadedPlacesPacket()
                    {
                        Places = poi.Places
                            .All
                            .Where(TagQuery.Parse(packet.TagQueriesText))
                            .Select(x => (ProtoPlace)x)
                            .ToList(),
                    },
                    fromPlayer);
            });

        _serverNetworkChannel.SetMessageHandler(
            (IServerPlayer fromPlayer, SavePlacesPacket packet) =>
            {
                List<Place> newPlaces = (packet.Places ?? []).SelectNonNulls(x => (Place?)x).ToList();

                PlayerPlacesOfInterest poi = new(fromPlayer);
                poi.Places.Import(newPlaces, packet.ExistingPlaceAction);

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
                    PlayerPlacesOfInterest poi = new(args.Caller.Player);
                    poi.Places.Clear();

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
                    PlayerPlacesOfInterest poi = new(args.Caller.Player);

                    TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");

                    Places placesCloseToPlayer = poi.Places.All.AtPlayerPosition();

                    if (placesCloseToPlayer.Count == 0 && !tagQuery.IncludedTagNames.Any())
                    {
                        return TextCommandResult.Success(
                            Lang.Get("places-of-interest-mod:interestingCommandResultNothingToAdd"));
                    }

                    placesCloseToPlayer.Update(
                        tagQuery,
                        args.Caller.Pos,
                        allowRemove: true,
                        allowChange: true,
                        allowAdd: true,
                        out int _,
                        out int _,
                        out int _);

                    poi.Places.Save();

                    if (placesCloseToPlayer.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:interestingCommandResult",
                                FormTagsText(tagQuery.IncludedTagNames, [], [], [])));
                    }
                    else
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:interestingCommandResultUpdated",
                                FormTagsText(
                                    tagQuery.IncludedTagNames,
                                    [],
                                    tagQuery.ExcludedTagNames,
                                    tagQuery.ExcludedTagPatterns)));
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
                    PlayerPlacesOfInterest poi = new(args.Caller.Player);

                    TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");

                    Places matchingPlaces = poi.Places.All.Where(tagQuery);

                    if (matchingPlaces.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:noMatchingPlacesFound",
                                FormTagsText(
                                    tagQuery.IncludedTagNames,
                                    tagQuery.IncludedTagPatterns,
                                    tagQuery.ExcludedTagNames,
                                    tagQuery.ExcludedTagPatterns)));
                    }

                    Place nearestPlace = matchingPlaces.FindNearestPlace();

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:foundNearestPlace",
                            FormTagsText(nearestPlace.CalculateActiveTagNames(poi.Calendar.Today), [], [], []),
                            (int)Math.Round(poi.Places.CalculateHorizontalDistance(nearestPlace)),
                            (int)Math.Round(poi.Places.CalculateVerticalDistance(nearestPlace))));
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

                    PlayerPlacesOfInterest poi = new(args.Caller.Player);

                    Places placesInRadius = poi.Places.All.AroundPlayer(searchRadius);

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

                    PlayerPlacesOfInterest poi = new(args.Caller.Player);

                    (TagQuery searchTagQuery, TagQuery updateTagQuery) = TagQuery.ParseSearchAndUpdate(args.LastArg?.ToString() ?? "");

                    Places placesCloseToPlayer = poi.Places.All.AroundPlayer(searchRadius);

                    if (placesCloseToPlayer.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:editInterestingPlacesResultNoPlacesFound",
                                searchRadius,
                                FormTagsText(
                                    searchTagQuery.IncludedTagNames,
                                    searchTagQuery.IncludedTagPatterns,
                                    searchTagQuery.ExcludedTagNames,
                                    searchTagQuery.ExcludedTagPatterns)));
                    }

                    Places matchingPlaces = placesCloseToPlayer.Where(searchTagQuery);

                    matchingPlaces.Update(
                        updateTagQuery,
                        poi.XYZ,
                        true,
                        true,
                        true,
                        out int numberOfRemovedPlaces,
                        out int numberOfChangedPlaces,
                        out int _);

                    poi.Places.Save();

                    if (matchingPlaces.Count == 0)
                    {
                        return TextCommandResult.Success(
                            Lang.Get(
                                "places-of-interest-mod:editInterestingPlacesResultNoPlacesFound",
                                searchRadius,
                                FormTagsText(
                                    searchTagQuery.IncludedTagNames,
                                    searchTagQuery.IncludedTagPatterns,
                                    searchTagQuery.ExcludedTagNames,
                                    searchTagQuery.ExcludedTagPatterns)));
                    }

                    return TextCommandResult.Success(
                        Lang.Get(
                            "places-of-interest-mod:editInterestingPlacesResultUpdatedPlaces",
                            matchingPlaces.Count,
                            FormTagsText(
                                updateTagQuery.IncludedTagNames,
                                updateTagQuery.IncludedTagPatterns,
                                updateTagQuery.ExcludedTagNames,
                                updateTagQuery.ExcludedTagPatterns),
                            numberOfRemovedPlaces,
                            numberOfChangedPlaces));
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
        //             PlayerPlacesOfInterest poi = new(args.Caller.Player);
        //
        //             return TextCommandResult.Success(poi.Today.ToString());
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
