using FluentAssertions;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Tests for the /dist command (findInterestingPlace).
/// </summary>
/// <remarks>
/// PlayerMock should be created for each ServerChatCommands.HandleCommandFindInterestingPlace call.
/// It is only there to fake VintageStory API and each command call should use a fresh instance.
/// </remarks>
public class DistCommandTests
{
    [Fact]
    public void NoPlacesExist_ReturnsNoMatchingPlacesFound()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "copper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.noMatchingPlacesFound);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
    }

    [Fact]
    public void SinglePlaceWithMatchingTag_ReturnsPlaceWithDistance()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "copper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[0].Should().Be("copper");
        commandResult.AsSuccess.Args[1].Should().Be(100);
        commandResult.AsSuccess.Args[2].Should().Be(0);
    }

    [Fact]
    public void PlaceWithNonMatchingTag_ReturnsNoMatchingPlacesFound()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 50), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "gold");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.noMatchingPlacesFound);
        commandResult.AsSuccess.Args.Should().HaveCount(1);
    }

    [Fact]
    public void MultiplePlacesWithSameTag_ReturnsNearestPlace()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "ore");
        playerData.AddPlace(playerData.Place(50, 0, 0), "ore");
        playerData.AddPlace(playerData.Place(200, 0, 0), "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(50, "should find the closest place at 50 blocks");
    }

    [Fact]
    public void PlaceWithMultipleTags_MatchesAnyTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "copper", "tin", "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "tin");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        string tagsResult = (string)commandResult.AsSuccess.Args[0];
        tagsResult.Split(' ').Should().Contain("tin");
    }

    [Fact]
    public void SearchWithMultipleTags_FindsPlaceMatchingAllTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(75, 0, 0), "ore");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "copper ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(100, "only the place at 100 blocks has both tags");
    }

    [Fact]
    public void SearchWithExcludedTag_FiltersOutPlacesWithThatTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "copper", "depleted");
        playerData.AddPlace(playerData.Place(100, 0, 0), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "copper -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(100, "should skip the closer depleted place");
    }

    [Fact]
    public void VerticalDistance_IsCalculatedCorrectly()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(0, 50, 0), "cave");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "cave");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(0, "horizontal distance should be 0");
        commandResult.AsSuccess.Args[2].Should().Be(50, "vertical distance should be 50");
    }

    [Fact]
    public void HorizontalAndVerticalDistance_BothCalculated()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(80, 60, 0), "mountain");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "mountain");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(80, "horizontal distance (XZ plane only)");
        commandResult.AsSuccess.Args[2].Should().Be(60, "vertical distance");
    }

    [Fact]
    public void PlaceAtPlayerLocation_ReturnsZeroDistance()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "home");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "home");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(0, "horizontal distance should be 0");
        commandResult.AsSuccess.Args[2].Should().Be(0, "vertical distance should be 0");
    }

    [Fact]
    public void PlayerMovesCloserToPlace_UpdatesDistance()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "destination");

        // First measurement
        PlayerMock playerMock1 = new(playerData);
        LocalizedTextCommandResult commandResult1 = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock1.Object, "destination");

        commandResult1.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult1.AsSuccess.Args[1].Should().Be(100);

        // Move player closer
        playerData.MoveToDifferentPlace(50, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        LocalizedTextCommandResult commandResult2 = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock2.Object, "destination");

        commandResult2.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult2.AsSuccess.Args[1].Should().Be(50, "distance should decrease as player moves closer");
    }

    [Fact]
    public void NegativeCoordinates_CalculatesDistanceCorrectly()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(-100, -30, -50), "underground");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "underground");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        ((int)commandResult.AsSuccess.Args[1]).Should().BeGreaterThan(0, "horizontal distance is always positive");
        ((int)commandResult.AsSuccess.Args[2]).Should().Be(-30, "vertical distance can be negative when place is below player");
    }

    [Fact]
    public void SearchWithPattern_MatchesPlacesWithPatternInTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "copper_ore");
        playerData.AddPlace(playerData.Place(50, 0, 0), "tin");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "copper_ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
        commandResult.AsSuccess.Args[1].Should().Be(100);
    }

    [Fact]
    public void EmptyTagSearch_WithExistingPlace_FindsPlace()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(100, 0, 0), "copper");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandFindInterestingPlace(playerMock.Object, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.foundNearestPlace);
        commandResult.AsSuccess.Args.Should().HaveCount(3);
    }
}
