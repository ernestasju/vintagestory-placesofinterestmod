using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlacesOfInterestMod;

public sealed class ClientSide : IClientSide
{
    private readonly ICoreClientAPI _clientApi;
    private readonly IClientNetworkChannel _clientNetworkChannel;

    public ClientSide(ICoreClientAPI clientApi, IClientNetworkChannel clientNetworkChannel)
    {
        ArgumentNullException.ThrowIfNull(clientApi);
        ArgumentNullException.ThrowIfNull(clientNetworkChannel);

        _clientApi = clientApi;
        _clientNetworkChannel = clientNetworkChannel;
    }

    public void Register()
    {
        ArgumentNullException.ThrowIfNull(_clientApi);

        _clientNetworkChannel.RegisterMessageType<LoadPlacesPacket>();
        _clientNetworkChannel.RegisterMessageType<LoadedPlacesPacket>();
        _clientNetworkChannel.RegisterMessageType<SavePlacesPacket>();
        _clientNetworkChannel.RegisterMessageType<SavedPlacesPacket>();

        _clientNetworkChannel.SetMessageHandler(
            (LoadedPlacesPacket packet) => HandleNetworkPacket(this, packet));

        _clientNetworkChannel.SetMessageHandler(
            (SavedPlacesPacket packet) => HandleNetworkPacket(this, packet));

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

                    return HandleChatCommandCopyInterestingPlaces(this, searchRadius, tagQueriesText);
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
                    // NOTE: Arg is guaranteed to exist.
                    string? existingPlaceActionText = args[0] as string;

                    ExistingPlaceAction existingPlaceAction = existingPlaceActionText?.ToLower() switch
                    {
                        "skip" => ExistingPlaceAction.Skip,
                        "update" => ExistingPlaceAction.Update,
                        "replace" => ExistingPlaceAction.Replace,
                        _ => ExistingPlaceAction.Skip,
                    };

                    return HandleChatCommandPasteInterestingPlaces(this, existingPlaceAction);
                });
    }

    public static void HandleNetworkPacket(
        IClientSide clientSide,
        LoadedPlacesPacket packet)
    {
        List<SerializablePlace> places = (packet.Places ?? [])
            .SelectNonNulls(x => (Place?)x)
            .Select(x => (SerializablePlace)x)
            .ToList();

        clientSide.SetClipboardText(JsonSerializer.Serialize(places));

        clientSide.TriggerChatMessage(
            new LocalizedText(LocalizedTexts.copyInterestingPlacesResult, places.Count));
    }

    public static void HandleNetworkPacket(
        IClientSide clientSide,
        SavedPlacesPacket packet)
    {
        clientSide.TriggerChatMessage(
            new LocalizedText(LocalizedTexts.pasteInterestingPlacesResult, packet.PlacesCount));
    }

    public static LocalizedTextCommandResult HandleChatCommandCopyInterestingPlaces(
        IClientSide clientSide,
        int searchRadius,
        string tagQueriesText)
    {
        clientSide.SendNetworkPacketToServerSide(new LoadPlacesPacket()
        {
            SearchRadius = searchRadius,
            TagQueriesText = tagQueriesText,
        });

        return LocalizedTextCommandResult.Success(
            new(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress));
    }

    public static LocalizedTextCommandResult HandleChatCommandPasteInterestingPlaces(
        IClientSide clientSide,
        ExistingPlaceAction existingPlaceAction)
    {
        string? clipboardText = clientSide.GetClipboardText() is string value && !string.IsNullOrWhiteSpace(value) ? value : null;

        if (clipboardText is null)
        {
            return LocalizedTextCommandResult.Success(
                new(LocalizedTexts.pasteInterestingPlacesResultNoClipboard));
        }

        List<SerializablePlace>? serializablePlaces;
        try
        {
            serializablePlaces = JsonSerializer.Deserialize<List<SerializablePlace>>(clipboardText);
        }
        catch (Exception)
        {
            return LocalizedTextCommandResult.Success(
                new(LocalizedTexts.pasteInterestingPlacesResultInvalidClipboard));
        }

        serializablePlaces ??= [];

        List<ProtoPlace> newPlaces = serializablePlaces
            .SelectNonNulls(x => (Place?)x)
            .Select(x => (ProtoPlace)x)
            .ToList();

        clientSide.SendNetworkPacketToServerSide(
            new SavePlacesPacket()
            {
                ExistingPlaceAction = existingPlaceAction,
                Places = newPlaces,
            });

        return LocalizedTextCommandResult.Success(
            new(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress));
    }

    public string? GetClipboardText()
    {
        return _clientApi.Forms.GetClipboardText();
    }

    public void SetClipboardText(string text)
    {
        _clientApi.Forms.SetClipboardText(text);
    }

    public void TriggerChatMessage(LocalizedText localizedText)
    {
        _clientApi.TriggerChatMessage(localizedText);
    }

    public void SendNetworkPacketToServerSide<T>(T packet)
    {
        _clientNetworkChannel.SendPacket(packet);
    }
}
