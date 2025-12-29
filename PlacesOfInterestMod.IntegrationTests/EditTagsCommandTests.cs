using FluentAssertions;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Tests for the /editTags command (editInterestingPlaces).
/// </summary>
/// <remarks>
/// PlayerMock should be created for each ServerChatCommands.HandleCommandEditPlaces call.
/// It is only there to fake VintageStory API and each command call should use a fresh instance.
/// </remarks>
public class EditTagsCommandTests
{
    [Fact]
    public void NoPlacesInRadius_ReturnsNoPlacesFound()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "-> copper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultNoPlacesFound);
        commandResult.AsSuccess.Args.Should().HaveCount(2);
        commandResult.AsSuccess.Args[0].Should().Be(100);
    }

    [Fact]
    public void PlacesExistButOutsideRadius_ReturnsNoPlacesFound()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(200, 0, 0), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "-> tin");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultNoPlacesFound);
        commandResult.AsSuccess.Args[0].Should().Be(100);
    }

    [Fact]
    public void NoSearchCriteriaUpdatesAllPlacesInRadius_AddsTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(75, 0, 0), "tin");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "-> ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args.Should().HaveCount(4);
        commandResult.AsSuccess.Args[0].Should().Be(2, "both places should be updated");

        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("ore");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().Contain("ore");
    }

    [Fact]
    public void SearchByTag_UpdatesOnlyMatchingPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(75, 0, 0), "tin", "ore");
        playerData.AddPlace(playerData.Place(25, 0, 0), "coal");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "ore -> rich");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(2, "only places with 'ore' tag should be updated");

        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("rich");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().Contain("rich");
        playerData.ProtoPlaces[2].Tags!.Select(x => x.Name).Should().NotContain("rich");
    }

    [Fact]
    public void AddTagToMatchingPlaces_PreservesExistingTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper -> rich");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("copper", "ore", "rich");
    }

    [Fact]
    public void RemoveTagFromPlaces_RemovesOnlySpecifiedTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore", "depleted");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper -> -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("copper", "ore");
    }

    [Fact]
    public void RemoveAllTagsFromPlace_DeletesPlace()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "depleted");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper depleted -> -copper -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[2].Should().Be(1, "one place should be removed");
        playerData.ProtoPlaces.Should().BeEmpty("place with no tags should be deleted");
    }

    [Fact]
    public void AddAndRemoveTagsSimultaneously_UpdatesPlaceCorrectly()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore", "depleted");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "-> rich -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("copper", "ore", "rich");
    }

    [Fact]
    public void SearchMultipleTags_UpdatesPlacesMatchingAllTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(75, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(25, 0, 0), "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper ore -> rich");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(1, "only the place with both tags should be updated");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("rich");
    }

    [Fact]
    public void SearchWithExcludedTag_ExcludesPlacesWithThatTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "depleted");
        playerData.AddPlace(playerData.Place(75, 0, 0), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper -depleted -> rich");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(1, "only non-depleted copper place should be updated");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("rich");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().Contain("rich");
    }

    [Fact]
    public void UpdatePlacesWithZeroRadius_UsesInfiniteRadius()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10000, 0, 0), "faraway");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, int.MaxValue, "-> visited");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(1);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("visited");
    }

    [Fact]
    public void UpdateMultiplePlaces_ReturnsCorrectCounts()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(75, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(25, 0, 0), "tin", "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper -> processed");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(2, "number of places found");
        commandResult.AsSuccess.Args[3].Should().Be(2, "number of places changed");
    }

    [Fact]
    public void NoMatchingPlacesInRadius_ReturnsNoPlacesFound()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(75, 0, 0), "tin");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "gold -> rich");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultNoPlacesFound);
        commandResult.AsSuccess.Args[0].Should().Be(100);
    }

    [Fact]
    public void EmptyUpdateQuery_DoesNotModifyPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper ->");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[3].Should().Be(0, "no changes should be made with empty update query");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("copper", "ore");
    }

    [Fact]
    public void ReplaceTag_RemovesOldAndAddsNew()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "ore", "unprocessed");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "-> processed -unprocessed");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("copper", "ore", "processed");
    }

    [Fact]
    public void UpdatePlaceAtPlayerLocation_UpdatesSuccessfully()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "home");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 10, "-> base");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("home", "base");
    }

    [Fact]
    public void SmallRadius_UpdatesOnlyNearbyPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "near");
        playerData.AddPlace(playerData.Place(50, 0, 0), "far");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 20, "-> visited");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(1, "only the nearby place should be updated");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("visited");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().NotContain("visited");
    }

    [Fact]
    public void UpdatePlacesWithPattern_MatchesPlacesWithPatternInTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper_ore", "rich");
        playerData.AddPlace(playerData.Place(75, 0, 0), "tin_ore", "rich");
        playerData.AddPlace(playerData.Place(25, 0, 0), "coal");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "~*ore -> -rich");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(2, "both ore places should match pattern");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("rich");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().NotContain("rich");
        playerData.ProtoPlaces[2].Tags!.Select(x => x.Name).Should().BeEquivalentTo("coal");
    }

    [Fact]
    public void UpdateMultiplePlacesRemovingAllTags_DeletesAllPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "temporary");
        playerData.AddPlace(playerData.Place(75, 0, 0), "temporary");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "temporary -> -temporary");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[2].Should().Be(2, "both places should be removed");
        playerData.ProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void MixOfUpdatesAndDeletions_ReturnsCorrectCounts()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "depleted");
        playerData.AddPlace(playerData.Place(75, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(25, 0, 0), "tin");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandEditPlaces(playerMock.Object, 100, "copper depleted -> -copper -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.editInterestingPlacesResultUpdatedPlaces);
        commandResult.AsSuccess.Args[0].Should().Be(1, "one place matched the search");
        commandResult.AsSuccess.Args[2].Should().Be(1, "one place was removed");
        playerData.ProtoPlaces.Should().HaveCount(2);
    }

    [Fact]
    public void PlayerMovesToDifferentArea_UpdatesOnlyNewAreaPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "area1", "landmark");
        playerData.AddPlace(playerData.Place(100, 0, 0), "area2", "landmark");

        // First update at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandEditPlaces(playerMock1.Object, 20, "landmark -> visited");

        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("visited");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().NotContain("visited");

        // Move to second area and update
        playerData.MoveToDifferentPlace(100, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        ServerSide.HandleChatCommandEditPlaces(playerMock2.Object, 20, "landmark -visited -> mapped");

        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().Contain("mapped");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("mapped");
    }
}
