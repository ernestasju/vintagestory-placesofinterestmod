using System;
using System.Collections.Generic;
using System.Linq;
using PlacesOfInterestMod.Generated;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlacesOfInterestMod;

public sealed class ServerSide
{
    private readonly ICoreServerAPI _serverApi;
    private readonly IServerNetworkChannel _serverNetworkChannel;

    public ServerSide(ICoreServerAPI serverApi, IServerNetworkChannel serverNetworkChannel)
    {
        ArgumentNullException.ThrowIfNull(serverApi);
        ArgumentNullException.ThrowIfNull(serverNetworkChannel);

        _serverApi = serverApi;
        _serverNetworkChannel = serverNetworkChannel;
    }

    public void Register()
    {
        _serverNetworkChannel.RegisterMessageType<LoadPlacesPacket>();
        _serverNetworkChannel.RegisterMessageType<LoadedPlacesPacket>();
        _serverNetworkChannel.RegisterMessageType<SavePlacesPacket>();
        _serverNetworkChannel.RegisterMessageType<SavedPlacesPacket>();

        _serverNetworkChannel.SetMessageHandler(
            (IServerPlayer fromPlayer, LoadPlacesPacket packet) =>
            {
                VintageStoryServerPlayer serverPlayer = new(fromPlayer, _serverNetworkChannel);
                HandleNetworkPacket(serverPlayer, packet);
            });

        _serverNetworkChannel.SetMessageHandler(
            (IServerPlayer fromPlayer, SavePlacesPacket packet) =>
            {
                VintageStoryServerPlayer serverPlayer = new(fromPlayer, _serverNetworkChannel);
                HandleNetworkPacket(serverPlayer, packet);
            });

        _ = _serverApi.ChatCommands.Create()
            .WithName("clearInterestingPlaces")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.clearInterestingPlacesCommandDescription))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleChatCommandClearInterestingPlaces(player);
                });

        _ = _serverApi.ChatCommands.Create()
            .WithName("interesting")
            .WithAlias("tag")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.interestingCommandDescription))
            .WithExamples(
                Lang.Get(LocalizedTexts.interestingCommandExample1),
                Lang.Get(LocalizedTexts.interestingCommandExample2))
            .WithArgs(
                _serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleChatCommandTagInterestingPlace(player, args.LastArg?.ToString() ?? "");
                });

        _serverApi.ChatCommands.Create()
            .WithName("findInterestingPlace")
            .WithAlias("dist")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.findInterestingPlaceCommandDescription))
            .WithArgs(
                _serverApi.ChatCommands.Parsers.All("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleChatCommandFindInterestingPlace(player, args.LastArg?.ToString() ?? "");
                });

        _serverApi.ChatCommands.Create()
            .WithName("whatsSoInteresting")
            .WithAlias("tags")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.whatsSoInterestingCommandDescription))
            .WithArgs(
                _serverApi.ChatCommands.Parsers.OptionalInt("radius", 100),
                _serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);

                    // NOTE: Arg is guaranteed to exist.
                    return HandleChatCommandFindTagsAroundPlayer(player, (int)args[0], args.LastArg?.ToString() ?? "");
                });

        _serverApi.ChatCommands.Create()
            .WithName("editInterestingPlaces")
            .WithAlias("editTags")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.editInterestingPlacesCommandDescription))
            .WithExamples(
                Lang.Get(LocalizedTexts.editInterestingPlacesCommandExample1),
                Lang.Get(LocalizedTexts.editInterestingPlacesCommandExample2),
                Lang.Get(LocalizedTexts.editInterestingPlacesCommandExample3))
            .WithArgs(
                _serverApi.ChatCommands.Parsers.OptionalInt("radius", 16),
                _serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);

                    // NOTE: Arg is guaranteed to exist.
                    return HandleChatCommandEditPlaces(player, (int)args[0], args.LastArg?.ToString() ?? "");
                });

        // NOTE: Commented for later.
        // serverApi.ChatCommands.Create()
        //     .WithName("today")
        //     .RequiresPlayer()
        //     .RequiresPrivilege(Privilege.chat)
        //     .WithDescription("Shows current day")
        //     .HandleWith(
        //          (TextCommandCallingArgs args) =>
        //          {
        //              PlayerPlacesOfInterest poi = new(args.Caller.Player);
        //              return TextCommandResult.Success(poi.Calendar.Today.ToString());
        //          });
    }

    public static void HandleNetworkPacket(
        IVintageStoryServerPlayer serverPlayer,
        LoadPlacesPacket packet)
    {
        PlayerPlacesOfInterest poi = new(serverPlayer.Player);

        serverPlayer.SendPacket(
            new LoadedPlacesPacket()
            {
                Places = poi.Places
                    .All
                    .AroundPlayer(packet.SearchRadius)
                    .Where(TagQuery.Parse(packet.TagQueriesText))
                    .Select(x => (ProtoPlace)x)
                    .ToList(),
            });
    }

    public static void HandleNetworkPacket(
        IVintageStoryServerPlayer serverPlayer,
        SavePlacesPacket packet)
    {
        List<Place> newPlaces = (packet.Places ?? []).SelectNonNulls(x => (Place?)x).ToList();

        PlayerPlacesOfInterest poi = new(serverPlayer.Player);
        poi.Places.Import(newPlaces, packet.ExistingPlaceAction);

        serverPlayer.SendPacket(
            new SavedPlacesPacket()
            {
                PlacesCount = newPlaces.Count,
            });
    }

    public static LocalizedTextCommandResult HandleChatCommandClearInterestingPlaces(
        IVintageStoryPlayer player)
    {
        PlayerPlacesOfInterest poi = new(player);
        poi.Places.Clear();

        return LocalizedTextCommandResult.Success(new(LocalizedTexts.clearedInterestingPlaces));
    }

    public static LocalizedTextCommandResult HandleChatCommandTagInterestingPlace(
        IVintageStoryPlayer player,
        string tags)
    {
        PlayerPlacesOfInterest poi = new(player);
        TagQuery tagQuery = TagQuery.Parse(tags);

        Places placesCloseToPlayer = poi.Places.All.AtPlayerPosition();

        if (placesCloseToPlayer.Count == 0 && !tagQuery.IncludedTagNames.Any())
        {
            return LocalizedTextCommandResult.Success(new(LocalizedTexts.interestingCommandResultNothingToAdd));
        }

        if (placesCloseToPlayer.Count > 0 && !tagQuery.IncludedTagNames.Any() && !tagQuery.ExcludedTagNames.Any() && !tagQuery.ExcludedTagPatterns.Any())
        {
            return LocalizedTextCommandResult.Success(new(LocalizedTexts.interestingCommandResultNothingToAddChangeOrRemove));
        }

        placesCloseToPlayer.Update(
            tagQuery,
            poi.XYZ,
            allowRemove: true,
            allowChange: true,
            allowAdd: true,
            out int _,
            out int _,
            out int _);

        poi.Places.Save();

        if (placesCloseToPlayer.Count == 0)
        {
            return LocalizedTextCommandResult.Success(new(
                LocalizedTexts.interestingCommandResult,
                Chat.FormTagsText(tagQuery.IncludedTagNames, [], [], [])));
        }
        else
        {
            return LocalizedTextCommandResult.Success(new(
                LocalizedTexts.interestingCommandResultUpdated,
                Chat.FormTagsText(
                    tagQuery.IncludedTagNames,
                    [],
                    tagQuery.ExcludedTagNames,
                    tagQuery.ExcludedTagPatterns)));
        }
    }

    public static LocalizedTextCommandResult HandleChatCommandFindInterestingPlace(
        IVintageStoryPlayer player,
        string tags)
    {
        PlayerPlacesOfInterest poi = new(player);
        TagQuery tagQuery = TagQuery.Parse(tags);

        Places matchingPlaces = poi.Places.All.Where(tagQuery);

        if (matchingPlaces.Count == 0)
        {
            return LocalizedTextCommandResult.Success(new(
                LocalizedTexts.noMatchingPlacesFound,
                Chat.FormTagsText(
                    tagQuery.IncludedTagNames,
                    tagQuery.IncludedTagPatterns,
                    tagQuery.ExcludedTagNames,
                    tagQuery.ExcludedTagPatterns)));
        }

        Place nearestPlace = matchingPlaces.FindNearestPlace();

        return LocalizedTextCommandResult.Success(new(
            LocalizedTexts.foundNearestPlace,
            Chat.FormTagsText(nearestPlace.CalculateActiveTagNames(poi.Calendar.Today), [], [], []),
            (int)Math.Round(poi.Places.CalculateHorizontalDistance(nearestPlace)),
            (int)Math.Round(poi.Places.CalculateVerticalDistance(nearestPlace))));
    }

    public static LocalizedTextCommandResult HandleChatCommandFindTagsAroundPlayer(
        IVintageStoryPlayer player,
        int searchRadius,
        string tags)
    {
        if (searchRadius <= 0)
        {
            searchRadius = 16;
        }

        PlayerPlacesOfInterest poi = new(player);
        (TagQuery searchTagQuery, TagQuery filterTagQuery) = TagQuery.ParseSearchPlacesAndFilterTags(tags);

        Places matchingPlaces = poi.Places.All
            .AroundPlayer(searchRadius)
            .Where(searchTagQuery);

        HashSet<TagName> uniqueTags = matchingPlaces.ActiveTags
            .Where(x => filterTagQuery.TestTag(x))
            .ToHashSet();

        if (uniqueTags.Count == 0)
        {
            return LocalizedTextCommandResult.Success(new(LocalizedTexts.noInterestingTagsFound));
        }

        return LocalizedTextCommandResult.Success(new(
            LocalizedTexts.whatsSoInterestingResult,
            Chat.FormTagsText(uniqueTags, [], [], [])));
    }

    public static LocalizedTextCommandResult HandleChatCommandEditPlaces(
        IVintageStoryPlayer player,
        int searchRadius,
        string tags)
    {
        if (searchRadius <= 0)
        {
            searchRadius = int.MaxValue;
        }

        PlayerPlacesOfInterest poi = new(player);
        (TagQuery searchTagQuery, TagQuery updateTagQuery) = TagQuery.ParseSearchAndUpdate(tags);

        Places placesCloseToPlayer = poi.Places.All.AroundPlayer(searchRadius);

        if (placesCloseToPlayer.Count == 0)
        {
            return LocalizedTextCommandResult.Success(new(
                LocalizedTexts.editInterestingPlacesResultNoPlacesFound,
                searchRadius,
                Chat.FormTagsText(
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
            return LocalizedTextCommandResult.Success(new(
                LocalizedTexts.editInterestingPlacesResultNoPlacesFound,
                searchRadius,
                Chat.FormTagsText(
                    searchTagQuery.IncludedTagNames,
                    searchTagQuery.IncludedTagPatterns,
                    searchTagQuery.ExcludedTagNames,
                    searchTagQuery.ExcludedTagPatterns)));
        }

        return LocalizedTextCommandResult.Success(new(
            LocalizedTexts.editInterestingPlacesResultUpdatedPlaces,
            matchingPlaces.Count,
            Chat.FormTagsText(
                updateTagQuery.IncludedTagNames,
                updateTagQuery.IncludedTagPatterns,
                updateTagQuery.ExcludedTagNames,
                updateTagQuery.ExcludedTagPatterns),
            numberOfRemovedPlaces,
            numberOfChangedPlaces));
    }
}
