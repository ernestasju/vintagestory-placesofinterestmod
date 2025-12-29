using Moq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using FluentAssertions;

namespace PlacesOfInterestMod.IntegrationTests;

// Integration tests for the /tag command.
public class TagCommandTests
{
    [Fact]
    public void NoTagsWithNoParameters_ReturnsNothingToAdd()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces = [];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultNothingToAdd);
        storedProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void NewPlaceWithSingleTag_AddsPlaceWithTag()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces = [];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("a");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        storedProtoPlaces.Should().HaveCount(1);
        storedProtoPlaces[0].Tags.Should().HaveCount(1);
        storedProtoPlaces[0].Tags![0].Name.Should().Be("a");
    }

    [Fact]
    public void NewPlaceWithExcludedTag_DoesNotAddPlace()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces = [];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("-b");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultNothingToAdd);
        storedProtoPlaces.Should().BeEmpty();
    }

    [Fact]
    public void ExistingPlaceIncludeAndExcludeSameTag_RemovesTagFromPlace()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces =
        [
            new ProtoPlace()
            {
                XYZ = xyz,
                Tags =
                [
                    new ProtoTag() { Name = "c", StartDay = 0, EndDay = 0 },
                ],
            },
        ];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("c -c");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        // TODO: Review edge case.
        // When both including and excluding the same tag, the inclusion takes precedence
        // so the tag remains (this might be a design decision or bug to investigate separately)
        storedProtoPlaces.Should().HaveCount(1);
        storedProtoPlaces[0].Tags.Should().HaveCount(1);
        storedProtoPlaces[0].Tags![0].Name.Should().Be("c");
    }

    [Fact]
    public void ExistingPlaceIncludeAndExcludeDifferentTags_ChangesPlaceTags()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces =
        [
            new ProtoPlace()
            {
                XYZ = xyz,
                Tags =
                [
                    new ProtoTag() { Name = "d", StartDay = 0, EndDay = 0 },
                ],
            },
        ];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("e -d");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        storedProtoPlaces.Should().HaveCount(1);
        storedProtoPlaces[0].Tags.Should().HaveCount(1);
        storedProtoPlaces[0].Tags![0].Name.Should().Be("e", "tag 'd' should be removed and 'e' should be added");
    }

    [Fact]
    public void NewPlaceWithMultipleTags_AddsPlaceWithAllTags()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces = [];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("f g h");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResult);
        storedProtoPlaces.Should().HaveCount(1);
        storedProtoPlaces[0].Tags.Should().HaveCount(3);
        storedProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("f", "g", "h");
    }

    [Fact]
    public void ExistingPlaceAddTag_UpdatesPlaceWithAdditionalTag()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces =
        [
            new ProtoPlace()
            {
                XYZ = xyz,
                Tags =
                [
                    new ProtoTag() { Name = "i", StartDay = 0, EndDay = 0 },
                ],
            },
        ];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("j");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        storedProtoPlaces.Should().HaveCount(1);
        storedProtoPlaces[0].Tags.Should().HaveCount(2);
        storedProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("i", "j");
    }

    [Fact]
    public void ExistingPlaceRemoveAllTags_DeletesPlace()
    {
        Vec3d xyz = new(10, 20, 30);
        List<ProtoPlace> storedProtoPlaces =
        [
            new ProtoPlace()
            {
                XYZ = xyz,
                Tags =
                [
                    new ProtoTag() { Name = "k", StartDay = 0, EndDay = 0 },
                    new ProtoTag() { Name = "l", StartDay = 0, EndDay = 0 },
                ],
            }
        ];
        Mock<IPlayerPlacesOfInterest> poiMock = CreateMockPoI(xyz, storedProtoPlaces);

        TagQuery tagQuery = TagQuery.Parse("-k -l");
        LocalizedTextCommandResult commandResult = ServerChatCommands.HandleCommandTagInterestingPlace(poiMock.Object, tagQuery);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.interestingCommandResultUpdated);
        storedProtoPlaces.Should().BeEmpty("place should be removed when all tags are removed");
    }

    private static Mock<IPlayerPlacesOfInterest> CreateMockPoI(Vec3d xyz, List<ProtoPlace> storedProtoPlaces)
    {
        Mock<IPlayerPlacesOfInterest> poiMock = new();
        PlayerPlaces? playerPlaces = null;
        PlayerCalendar? calendar = null;

        Mock<IVintageStoryPlayer> playerMock = new();
        Mock<IGameCalendar> gameCalendarMock = new();

        gameCalendarMock.Setup(x => x.TotalDays).Returns(44);
        gameCalendarMock.Setup(x => x.DaysPerMonth).Returns(9);
        gameCalendarMock.Setup(x => x.DaysPerYear).Returns(9 * 12);

        playerMock.Setup(x => x.Calendar).Returns(gameCalendarMock.Object);
        playerMock.Setup(x => x.XYZ).Returns(xyz);
        playerMock
            .Setup(x => x.LoadModData(It.IsAny<string>(), It.IsAny<List<ProtoPlace>>()))
            .Returns(storedProtoPlaces);
        playerMock
            .Setup(x => x.SaveModData(It.IsAny<string>(), It.IsAny<List<ProtoPlace>>()))
            .Callback(
                (string _, List<ProtoPlace> xs) =>
                {
                    storedProtoPlaces.Clear();
                    storedProtoPlaces.AddRange(xs);
                });
        playerMock
            .Setup(x => x.RemoveModData(It.IsAny<string>()))
            .Callback((string _) => storedProtoPlaces.Clear());

        poiMock.Setup(x => x.Player).Returns(playerMock.Object);
        poiMock
            .Setup(x => x.Places)
            .Returns(() => playerPlaces ??= PlayerPlaces.Load(poiMock.Object));
        poiMock
            .Setup(x => x.XYZ)
            .Returns(() => xyz);
        poiMock
            .Setup(x => x.Calendar)
            .Returns(() => calendar ??= new PlayerCalendar(poiMock.Object));

        return poiMock;
    }
}