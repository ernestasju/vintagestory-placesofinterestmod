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

        Mod.Logger.Notification("Hello from Places of Interest server side: " + Lang.Get(LocalizedTexts.hello));

        RegisterServerNetworkChannels();
        RegisterServerChatCommands();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        _clientApi = api;

        Mod.Logger.Notification("Hello from Places of Interest client side: " + Lang.Get(LocalizedTexts.hello));

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
                PlayerPlacesOfInterest poi = new(new VintageStoryPlayer(fromPlayer));

                _serverNetworkChannel.SendPacket(
                    new LoadedPlacesPacket()
                    {
                        Places = poi.Places
                            .All
                            .AroundPlayer(packet.SearchRadius)
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

                PlayerPlacesOfInterest poi = new(new VintageStoryPlayer(fromPlayer));
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

        ServerChatCommands.RegisterChatCommands(_serverApi);
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
                    LocalizedTexts.copyInterestingPlacesResult,
                    places.Count));
            });

        _clientNetworkChannel.SetMessageHandler(
            (SavedPlacesPacket packet) =>
            {
                _clientApi.TriggerChatMessage(Lang.Get(
                    LocalizedTexts.pasteInterestingPlacesResult,
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
            .WithDescription(Lang.Get(LocalizedTexts.copyInterestingPlacesCommandDescription))
            .WithExamples(
                Lang.Get(LocalizedTexts.copyInterestingPlacesCommandExample1),
                Lang.Get(LocalizedTexts.copyInterestingPlacesCommandExample2))
            .WithArgs(
                _clientApi.ChatCommands.Parsers.OptionalInt("radius", 0),
                _clientApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];

                    if (searchRadius <= 0)
                    {
                        searchRadius = int.MaxValue;
                    }

                    string tagQueriesText = args.LastArg?.ToString() ?? "";

                    _clientNetworkChannel.SendPacket(new LoadPlacesPacket()
                    {
                        SearchRadius = searchRadius,
                        TagQueriesText = tagQueriesText,
                    });

                    return TextCommandResult.Success(
                        Lang.Get(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress));
                });

        _ = _clientApi.ChatCommands.Create()
            .WithName("pasteInterestingPlaces")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.pasteInterestingPlacesCommandDescription))
            .WithExamples(
                Lang.Get(LocalizedTexts.pasteInterestingPlacesCommandExample1),
                Lang.Get(LocalizedTexts.pasteInterestingPlacesCommandExample2),
                Lang.Get(LocalizedTexts.pasteInterestingPlacesCommandExample3))
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
                            Lang.Get(LocalizedTexts.pasteInterestingPlacesResultNoClipboard));
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
                            Lang.Get(LocalizedTexts.pasteInterestingPlacesResultInvalidClipboard));
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
                        Lang.Get(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress));
                });
    }
}
