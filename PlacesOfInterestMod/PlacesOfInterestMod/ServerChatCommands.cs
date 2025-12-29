using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlacesOfInterestMod;

public static class ServerChatCommands
{
    public static void RegisterChatCommands(ICoreServerAPI serverApi)
    {
        _ = serverApi.ChatCommands.Create()
            .WithName("clearInterestingPlaces")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.clearInterestingPlacesCommandDescription))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleCommandClearInterestingPlaces(player);
                });

        _ = serverApi.ChatCommands.Create()
            .WithName("interesting")
            .WithAlias("tag")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.interestingCommandDescription))
            .WithExamples(
                Lang.Get(LocalizedTexts.interestingCommandExample1),
                Lang.Get(LocalizedTexts.interestingCommandExample2))
            .WithArgs(
                serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleCommandTagInterestingPlace(player, args.LastArg?.ToString() ?? "");
                });

        serverApi.ChatCommands.Create()
            .WithName("findInterestingPlace")
            .WithAlias("dist")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.findInterestingPlaceCommandDescription))
            .WithArgs(
                serverApi.ChatCommands.Parsers.All("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleCommandFindInterestingPlace(player, args.LastArg?.ToString() ?? "");
                });

        serverApi.ChatCommands.Create()
            .WithName("whatsSoInteresting")
            .WithAlias("tags")
            .RequiresPlayer()
            .RequiresPrivilege(Privilege.chat)
            .WithDescription(Lang.Get(LocalizedTexts.whatsSoInterestingCommandDescription))
            .WithArgs(
                serverApi.ChatCommands.Parsers.OptionalInt("radius", 100),
                serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];
                    if (searchRadius <= 0)
                    {
                        searchRadius = 16;
                    }

                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleCommandFindTagsAroundPlayer(player, searchRadius, args.LastArg?.ToString() ?? "");
                });

        serverApi.ChatCommands.Create()
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
                serverApi.ChatCommands.Parsers.OptionalInt("radius", 16),
                serverApi.ChatCommands.Parsers.OptionalAll("tags"))
            .HandleWith(
                TextCommandResult (TextCommandCallingArgs args) =>
                {
                    // NOTE: Arg is guaranteed to exist.
                    int searchRadius = (int)args[0];
                    if (searchRadius <= 0)
                    {
                        searchRadius = int.MaxValue;
                    }

                    VintageStoryPlayer player = new(args.Caller.Player);
                    return HandleCommandEditPlaces(player, searchRadius, args.LastArg?.ToString() ?? "");
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

    public static LocalizedTextCommandResult HandleCommandClearInterestingPlaces(
        IVintageStoryPlayer player)
    {
        PlayerPlacesOfInterest poi = new(player);
        poi.Places.Clear();

        return LocalizedTextCommandResult.Success(new(LocalizedTexts.clearedInterestingPlaces));
    }

    public static LocalizedTextCommandResult HandleCommandTagInterestingPlace(
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

    public static LocalizedTextCommandResult HandleCommandFindInterestingPlace(
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

    public static LocalizedTextCommandResult HandleCommandFindTagsAroundPlayer(
        IVintageStoryPlayer player,
        int searchRadius,
        string tags)
    {
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

    public static LocalizedTextCommandResult HandleCommandEditPlaces(
        IVintageStoryPlayer player,
        int searchRadius,
        string tags)
    {
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
