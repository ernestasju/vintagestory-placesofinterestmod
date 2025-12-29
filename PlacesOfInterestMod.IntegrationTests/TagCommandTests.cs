using FluentAssertions;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Tests for the /tag command.
/// </summary>
/// <remarks>
/// PlayerMock should be created for each ServerChatCommands.HandleCommandTagInterestingPlace call.
/// It is only there to fake VintageStory API and each command call should use a fresh instance.
/// </remarks>
public class TagCommandTests
{
    [Fact]
    public void NoTagsWithNoParameters_ReturnsNothingToAdd()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultNothingToAdd);
        playerData.ProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void NewPlaceWithSingleTag_AddsPlaceWithTag()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "a");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags![0].Name.Should().Be("a");
    }

    [Fact]
    public void NewPlaceWithExcludedTag_DoesNotAddPlace()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "-b");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultNothingToAdd);
        playerData.ProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void ExistingPlaceIncludeAndExcludeSameTag_RemovesTagFromPlace()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "c");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "c -c");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().HaveCount(1, "tag query always prioritizes tag additions");
        playerData.ProtoPlaces[0].Tags.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags![0].Name.Should().Be("c");
    }

    [Fact]
    public void ExistingPlaceIncludeAndExcludeDifferentTags_ChangesPlaceTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "d");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "e -d");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags![0].Name.Should().Be("e", "tag 'd' should be removed and 'e' should be added");
    }

    [Fact]
    public void NewPlaceWithMultipleTags_AddsPlaceWithAllTags()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "f g h");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags.Should().HaveCount(3);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("f", "g", "h");
    }

    [Fact]
    public void ExistingPlaceAddTag_UpdatesPlaceWithAdditionalTag()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "i");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "j");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags.Should().HaveCount(2);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("i", "j");
    }

    [Fact]
    public void ExistingPlaceRemoveAllTags_DeletesPlace()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "k", "l");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock.Object, "-k -l");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().BeEmpty("place should be removed when all tags are removed");
    }

    [Fact]
    public void PlayerMovesWithinSamePlace_UpdatesSamePlace()
    {
        TestPlayerData playerData = new();

        // Tag at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "area1");

        playerData.ProtoPlaces.Should().HaveCount(1);

        // Move to another position within the same place
        playerData.MoveAroundThePlace();
        PlayerMock playerMock2 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock2.Object, "area2");

        // Should update same place
        playerData.ProtoPlaces.Should().HaveCount(1, "moving within same place updates existing place");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("area1", "area2");
    }

    [Fact]
    public void PlayerMovesFarAway_CreatesNewPlace()
    {
        TestPlayerData playerData = new();

        // Tag at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "place1");

        playerData.ProtoPlaces.Should().HaveCount(1);

        // Move far away (10 place cells = 80 blocks)
        playerData.MoveToDifferentPlace(80, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock2.Object, "place2");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        playerData.ProtoPlaces.Should().HaveCount(2, "moving to different place creates new place");
        playerData.ProtoPlaces.Select(x => x.Tags![0].Name).Should().BeEquivalentTo("place1", "place2");
    }

    [Fact]
    public void PlayerReturnsToFirstPlace_UpdatesOriginalPlace()
    {
        TestPlayerData playerData = new();

        // Tag at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "home");

        // Move to different place and tag
        playerData.MoveToDifferentPlace(20, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock2.Object, "away");

        playerData.ProtoPlaces.Should().HaveCount(2);

        // Return to first place
        playerData.MoveToDifferentPlace(-20, 0, 0);
        playerData.MoveAroundThePlace();
        PlayerMock playerMock3 = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock3.Object, "base");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().HaveCount(2);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("home", "base");
        playerData.ProtoPlaces[1].Tags![0].Name.Should().Be("away");
    }

    [Fact]
    public void PlayerMovesVertically_CreatesNewPlaceAcrossVerticalBoundary()
    {
        TestPlayerData playerData = new();

        // Tag at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "surface");

        // Move 8 blocks up (crosses vertical place boundary)
        playerData.MoveToDifferentPlace(0, 8, 0);
        PlayerMock playerMock2 = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock2.Object, "upper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        playerData.ProtoPlaces.Should().HaveCount(2, "vertical movement to different place creates new place");
        playerData.ProtoPlaces[0].Tags![0].Name.Should().Be("surface");
        playerData.ProtoPlaces[1].Tags![0].Name.Should().Be("upper");
    }

    [Fact]
    public void PlayerMovesDiagonally_CreatesNewPlace()
    {
        TestPlayerData playerData = new();

        // Tag at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "corner1");

        // Move diagonally to a different place
        playerData.MoveToDifferentPlace(10, 10, 10);
        PlayerMock playerMock2 = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock2.Object, "corner2");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        playerData.ProtoPlaces.Should().HaveCount(2);
        playerData.ProtoPlaces[0].Tags![0].Name.Should().Be("corner1");
        playerData.ProtoPlaces[1].Tags![0].Name.Should().Be("corner2");
    }

    [Fact]
    public void PlayerTagsMultiplePlacesThenRemovesFromOne_OnlyAffectsCurrentPlace()
    {
        TestPlayerData playerData = new();

        // Tag first place
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "mine copper");

        // Move to second place and tag
        playerData.MoveToDifferentPlace(30, 0, 0);
        PlayerMock playerMock2 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock2.Object, "mine iron");

        playerData.ProtoPlaces.Should().HaveCount(2);

        // Return to first place and remove "copper" tag
        playerData.MoveToDifferentPlace(-30, 0, 0);
        playerData.MoveAroundThePlace();
        PlayerMock playerMock3 = new(playerData);
        LocalizedTextCommandResult commandResult = ServerSide
            .HandleChatCommandTagInterestingPlace(playerMock3.Object, "-copper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().HaveCount(2);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("mine");
        playerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().BeEquivalentTo("mine", "iron");
    }

    [Fact]
    public void PlayerMovesJustBeforeBoundary_UpdatesSamePlace()
    {
        TestPlayerData playerData = new();

        // Tag at starting position
        PlayerMock playerMock1 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock1.Object, "tag1");

        // Move to boundary of same place
        playerData.MoveToPlaceBoundary();
        PlayerMock playerMock2 = new(playerData);
        ServerSide.HandleChatCommandTagInterestingPlace(playerMock2.Object, "tag2");

        // Should still be in same place
        playerData.ProtoPlaces.Should().HaveCount(1, "moving to edge of same place updates existing place");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("tag1", "tag2");
    }
}
