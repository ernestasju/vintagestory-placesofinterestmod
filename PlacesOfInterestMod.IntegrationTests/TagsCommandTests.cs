using FluentAssertions;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Tests for the /tags command (whatsSoInteresting).
/// </summary>
/// <remarks>
/// PlayerMock should be created for each ServerChatCommands.HandleCommandFindTagsAroundPlayer call.
/// It is only there to fake VintageStory API and each command call should use a fresh instance.
/// </remarks>
public class TagsCommandTests
{
    [Fact]
    public void NoPlacesInArea_ReturnsNoTagsFound()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.noInterestingTagsFound);
    }

    [Fact]
    public void SinglePlaceWithinRadius_ReturnsTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
        commandResult.AsSuccess.Args[0].Should().Be("copper");
    }

    [Fact]
    public void MultiplePlacesWithinRadius_ReturnsAllUniqueTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper", "tin");
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper", "gold");
        playerData.AddPlace(playerData.Place(20, 0, 0), "silver");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Split(' ').Should().BeEquivalentTo("copper", "tin", "gold", "silver");
    }

    [Fact]
    public void PlaceOutsideRadius_IsNotIncluded()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "nearby");
        playerData.AddPlace(playerData.Place(51, 0, 0), "faraway");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 50, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Should().Be("nearby");
    }

    [Fact]
    public void PlayerMovesAndSearchAgain_FindsDifferentTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "area1");
        playerData.AddPlace(playerData.Place(15, 0, 0), "area2");

        // First search at starting position
        PlayerMock playerMock1 = new(playerData);
        LocalizedTextCommandResult commandResult1 = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock1.Object, 10, "");

        commandResult1.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult1 = (string)commandResult1.AsSuccess.Args[0];
        tagsResult1.Should().Be("area1");

        // Move player to second area
        playerData.MoveToDifferentPlace(15, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        LocalizedTextCommandResult commandResult2 = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock2.Object, 10, "");

        commandResult2.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult2 = (string)commandResult2.AsSuccess.Args[0];
        tagsResult2.Should().Be("area2");
    }

    [Fact]
    public void SearchWithTagFilter_ReturnsOnlyMatchingTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper", "ore");
        playerData.AddPlace(playerData.Place(10, 0, 0), "tin", "ore");
        playerData.AddPlace(playerData.Place(20, 0, 0), "village");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "-> ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Should().Be("ore");
    }

    [Fact]
    public void SearchWithExcludeFilter_OmitsMatchingTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper", "ore");
        playerData.AddPlace(playerData.Place(10, 0, 0), "tin", "ore");
        playerData.AddPlace(playerData.Place(20, 0, 0), "village");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "-> -ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Split(' ').Should().BeEquivalentTo("copper", "tin", "village");
    }

    [Fact]
    public void SearchPlacesByTagsAndFilterResults_WorksTogether()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper", "ore", "processed");
        playerData.AddPlace(playerData.Place(10, 0, 0), "tin", "ore");
        playerData.AddPlace(playerData.Place(20, 0, 0), "village");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "ore -> -ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Split(' ').Should().BeEquivalentTo("copper", "tin", "processed");
    }

    [Fact]
    public void ZeroRadius_FindsNothing()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(1, 0, 0), "nearby");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 0, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.noInterestingTagsFound);
    }

    [Fact]
    public void NegativeRadius_FindsNothing()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(0, 0, 0), "nearby");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, -10, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.noInterestingTagsFound);
    }

    [Fact]
    public void PlayerMovesToEmptyArea_ReturnsNoTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "oldarea");

        // First search finds tag
        PlayerMock playerMock1 = new(playerData);
        LocalizedTextCommandResult commandResult1 = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock1.Object, 20, "");

        commandResult1.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);

        // Move player far away
        playerData.MoveToDifferentPlace(50, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        LocalizedTextCommandResult commandResult2 = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock2.Object, 20, "");

        commandResult2.AsSuccess.Key.Should().Be(LocalizedTexts.noInterestingTagsFound);
    }

    [Fact]
    public void DuplicateTagsInMultiplePlaces_ReturnedOnlyOnce()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper");
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(20, 0, 0), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Should().Be("copper");
    }

    [Fact]
    public void SearchOnlyPlacesWithSpecificTag_ReturnsTagsFromThosePlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "mine", "copper");
        playerData.AddPlace(playerData.Place(20, 0, 0), "mine", "iron");
        playerData.AddPlace(playerData.Place(30, 0, 0), "village");

        // Debug: Check what we actually get
        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "mine");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Should().Be("mine");
    }

    [Fact]
    public void SearchWithMultipleIncludeTags_OnlyReturnsPlacesWithAllTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "mine", "copper", "processed");
        playerData.AddPlace(playerData.Place(20, 0, 0), "mine", "iron");
        playerData.AddPlace(playerData.Place(30, 0, 0), "copper", "raw");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 100, "mine copper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Split(' ').Should().BeEquivalentTo(["mine", "copper"], "should only return tags included and not excluded by \"filter tags\" query");
    }

    [Fact]
    public void SearchExcludingTag_OmitsPlacesWithThatTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "mine", "copper");
        playerData.AddPlace(playerData.Place(20, 0, 0), "mine", "depleted");
        playerData.AddPlace(playerData.Place(30, 0, 0), "village");

        PlayerMock player = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(player.Object, 100, "mine -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Should().Be("mine", "should only return tags included and not excluded by \"filter tags\" query");
    }

    [Fact]
    public void PlayerInDifferentPlace_SearchesAroundCurrentPosition()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "home");
        playerData.AddPlace(playerData.Place(10, 0, 0), "camp");
        playerData.AddPlace(playerData.Place(20, 0, 0), "mine");

        // Move to second place
        playerData.MoveToDifferentPlace(10, 0, 0);
        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 9, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Should().Be("camp");
    }

    [Fact]
    public void EmptyTagQuery_FindsAllPlacesWithinRadius()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "a");
        playerData.AddPlace(playerData.Place(10, 0, 0), "b");
        playerData.AddPlace(playerData.Place(20, 0, 0), "c");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindTagsAroundPlayer(playerMock.Object, 50, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.whatsSoInterestingResult);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Split(' ').Should().BeEquivalentTo("a", "b", "c");
    }
}
