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
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultNothingToAdd);
        playerData.ProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void NewPlaceWithSingleTag_AddsPlaceWithTag()
    {
        TestPlayerData playerData = new();

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "a");

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
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "-b");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultNothingToAdd);
        playerData.ProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void ExistingPlaceIncludeAndExcludeSameTag_RemovesTagFromPlace()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "c");

        PlayerMock playerMock = new(playerData);
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "c -c");

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
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "e -d");

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
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "f g h");

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
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "j");

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
        LocalizedTextCommandResult commandResult = ServerChatCommands
            .HandleCommandTagInterestingPlace(playerMock.Object, "-k -l");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        playerData.ProtoPlaces.Should().BeEmpty("place should be removed when all tags are removed");
    }
}
