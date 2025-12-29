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
                    PlayerPlacesOfInterest poi = new(args.Caller.Player);
                    return HandleCommandClearInterestingPlaces(poi);
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
                    PlayerPlacesOfInterest poi = new(args.Caller.Player);
                    TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");
                    return HandleCommandTagInterestingPlace(poi, tagQuery);
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
                    PlayerPlacesOfInterest poi = new(args.Caller.Player);
                    TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");
                    return HandleCommandFindInterestingPlace(poi, tagQuery);
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

                    PlayerPlacesOfInterest poi = new(args.Caller.Player);
                    (TagQuery searchTagQuery, TagQuery filterTagQuery) = TagQuery.ParseSearchPlacesAndFilterTags(args.LastArg?.ToString() ?? "");
                    return HandleCommandFindTagsAroundPlayer(poi, searchRadius, searchTagQuery, filterTagQuery);
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

                    PlayerPlacesOfInterest poi = new(args.Caller.Player);
                    (TagQuery searchTagQuery, TagQuery updateTagQuery) = TagQuery.ParseSearchAndUpdate(args.LastArg?.ToString() ?? "");
                    return HandleCommandEditPlaces(poi, searchRadius, searchTagQuery, updateTagQuery);
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
        IPlayerPlacesOfInterest poi)
    {
        poi.Places.Clear();

        return LocalizedTextCommandResult.Success(new(LocalizedTexts.clearedInterestingPlaces));
    }

    public static LocalizedTextCommandResult HandleCommandTagInterestingPlace(
        IPlayerPlacesOfInterest poi,
        TagQuery tagQuery)
    {
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
        IPlayerPlacesOfInterest poi,
        TagQuery tagQuery)
    {
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
        IPlayerPlacesOfInterest poi,
        int searchRadius,
        TagQuery searchTagQuery,
        TagQuery filterTagQuery)
    {
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
        IPlayerPlacesOfInterest poi,
        int searchRadius,
        TagQuery searchTagQuery,
        TagQuery updateTagQuery)
    {
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
