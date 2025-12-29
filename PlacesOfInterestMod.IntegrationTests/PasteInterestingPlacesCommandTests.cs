using System.Text.Json;
using FluentAssertions;
using Moq;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Helper class to simulate client-server communication for .pasteInterestingPlaces command.
/// </summary>
public sealed class PastePlacesTestHelper
{
    private readonly TestPlayerData _playerData;
    private readonly Mock<IClientSide> _clientSideMock;
    private readonly Mock<IVintageStoryServerPlayer> _serverPlayerMock;
    private SavePlacesPacket? _sentSavePacket;
    private SavedPlacesPacket? _receivedSavedPacket;
    private LocalizedText? _chatMessage;
    private string? _clipboardText;

    public PastePlacesTestHelper(TestPlayerData playerData)
    {
        _playerData = playerData;
        _clientSideMock = new Mock<IClientSide>();
        _serverPlayerMock = new Mock<IVintageStoryServerPlayer>();

        SetupClientSide();
        SetupServerSide();
    }

    public TestPlayerData PlayerData => _playerData;

    public SavePlacesPacket? SentSavePacket => _sentSavePacket;

    public SavedPlacesPacket? ReceivedSavedPacket => _receivedSavedPacket;

    public LocalizedText? ChatMessage => _chatMessage;

    public void SetClipboard(string clipboardContent)
    {
        _clipboardText = clipboardContent;
    }

    public void SetClipboardWithPlaces(params ProtoPlace[] places)
    {
        List<SerializablePlace> serializablePlaces = places
            .Select(p => (Place?)p)
            .SelectNonNulls(p => p)
            .Select(p => (SerializablePlace)p)
            .ToList();

        _clipboardText = JsonSerializer.Serialize(serializablePlaces);
    }

    private void SetupClientSide()
    {
        _clientSideMock
            .Setup(x => x.GetClipboardText())
            .Returns(() => _clipboardText);

        _clientSideMock
            .Setup(x => x.SendNetworkPacketToServerSide(It.IsAny<SavePlacesPacket>()))
            .Callback<SavePlacesPacket>(
                packet =>
                {
                    _sentSavePacket = packet;
                    SimulateServerProcessing(packet);
                });

        _clientSideMock
            .Setup(x => x.TriggerChatMessage(It.IsAny<LocalizedText>()))
            .Callback<LocalizedText>(msg => _chatMessage = msg);
    }

    private void SetupServerSide()
    {
        PlayerMock playerMock = new(_playerData);
        _serverPlayerMock.Setup(x => x.Player).Returns(playerMock.Object);

        _serverPlayerMock
            .Setup(x => x.SendPacket(It.IsAny<SavedPlacesPacket>()))
            .Callback<SavedPlacesPacket>(
                packet =>
                {
                    _receivedSavedPacket = packet;
                    SimulateClientReceivingPacket(packet);
                });
    }

    private void SimulateServerProcessing(SavePlacesPacket packet)
    {
        ServerSide.HandleNetworkPacket(_serverPlayerMock.Object, packet);
    }

    private void SimulateClientReceivingPacket(SavedPlacesPacket packet)
    {
        ClientSide.HandleNetworkPacket(_clientSideMock.Object, packet);
    }

    public LocalizedTextCommandResult ExecutePasteCommand(ExistingPlaceAction action)
    {
        return ClientSide.HandleChatCommandPasteInterestingPlaces(_clientSideMock.Object, action);
    }

    public void VerifyPacketSent()
    {
        _clientSideMock.Verify(
            x => x.SendNetworkPacketToServerSide(It.IsAny<SavePlacesPacket>()),
            Times.Once);
    }

    public void VerifyPacketNotSent()
    {
        _clientSideMock.Verify(
            x => x.SendNetworkPacketToServerSide(It.IsAny<SavePlacesPacket>()),
            Times.Never);
    }

    public void VerifyChatMessageSent()
    {
        _clientSideMock.Verify(x => x.TriggerChatMessage(It.IsAny<LocalizedText>()), Times.Once);
    }
}

/// <summary>
/// Tests for the .pasteInterestingPlaces command (client-side command with server-side processing).
/// </summary>
/// <remarks>
/// Flow:
/// 1. Client executes command, reads clipboard, deserializes JSON
/// 2. Client sends SavePlacesPacket to server with places and action
/// 3. Server processes packet, imports places based on action, sends SavedPlacesPacket back
/// 4. Client receives packet and triggers chat message with count
/// </remarks>
public class PasteInterestingPlacesCommandTests
{
    [Fact]
    public void PasteWithEmptyClipboard_ReturnsNoClipboardMessage()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultNoClipboard);
        helper.VerifyPacketNotSent();
    }

    [Fact]
    public void PasteWithInvalidJson_ReturnsInvalidClipboardMessage()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("this is not valid JSON");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultInvalidClipboard);
        helper.VerifyPacketNotSent();
    }

    [Fact]
    public void PasteWithValidJsonButNotPlaces_ReturnsInvalidClipboardMessage()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("{\"key\": \"value\"}");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultInvalidClipboard);
        helper.VerifyPacketNotSent();
    }

    [Fact]
    public void PasteWithEmptyArray_CompletesFullFlow()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("[]");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        helper.SentSavePacket.Should().NotBeNull();
        helper.SentSavePacket!.Places.Should().BeEmpty();
        helper.SentSavePacket.ExistingPlaceAction.Should().Be(ExistingPlaceAction.Skip);

        helper.ReceivedSavedPacket.Should().NotBeNull();
        helper.ReceivedSavedPacket!.PlacesCount.Should().Be(0);

        helper.ChatMessage.Should().NotBeNull();
        helper.ChatMessage!.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResult);
        helper.ChatMessage.Args[0].Should().Be(0);

        helper.VerifyPacketSent();
        helper.VerifyChatMessageSent();
    }

    [Fact]
    public void PasteWithSinglePlace_AddsPlaceToPlayerData()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags = [new ProtoTag { Name = "copper", StartDay = 0, EndDay = 0 }],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        helper.SentSavePacket!.Places.Should().HaveCount(1);
        helper.ReceivedSavedPacket!.PlacesCount.Should().Be(1);

        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("copper");

        helper.ChatMessage!.Args[0].Should().Be(1);
    }

    [Fact]
    public void PasteWithMultiplePlaces_AddsAllPlaces()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place1 = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags = [new ProtoTag { Name = "copper", StartDay = 0, EndDay = 0 }],
        };

        ProtoPlace place2 = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(300, 60, 400),
            Tags = [new ProtoTag { Name = "tin", StartDay = 0, EndDay = 0 }],
        };

        ProtoPlace place3 = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(500, 70, 600),
            Tags = [new ProtoTag { Name = "gold", StartDay = 0, EndDay = 0 }],
        };

        helper.SetClipboardWithPlaces(place1, place2, place3);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        helper.SentSavePacket!.Places.Should().HaveCount(3);
        helper.ReceivedSavedPacket!.PlacesCount.Should().Be(3);

        playerData.ProtoPlaces.Should().HaveCount(3);
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(t => t.Name).Should().Contain(["copper", "tin", "gold"]);

        helper.ChatMessage!.Args[0].Should().Be(3);
    }

    [Fact]
    public void PasteWithSkipAction_SkipsExistingPlaces()
    {
        TestPlayerData playerData = new();
        Vec3d targetXYZ = playerData.Place(100, 50, 200);
        playerData.AddPlace(targetXYZ, "existing");
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = targetXYZ,
            Tags = [new ProtoTag { Name = "new", StartDay = 0, EndDay = 0 }],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("existing");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("new");
    }

    [Fact]
    public void PasteWithUpdateAction_MergesTagsWithExistingPlaces()
    {
        TestPlayerData playerData = new();
        Vec3d targetXYZ = playerData.Place(100, 50, 200);
        playerData.AddPlace(targetXYZ, "existing", "ore");
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = targetXYZ,
            Tags = [new ProtoTag { Name = "new", StartDay = 0, EndDay = 0 }],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Update);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("existing");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("ore");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("new");
    }

    [Fact]
    public void PasteWithReplaceAction_ReplacesExistingPlaceTags()
    {
        TestPlayerData playerData = new();
        Vec3d targetXYZ = playerData.Place(100, 50, 200);
        playerData.AddPlace(targetXYZ, "existing", "ore", "old");
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = targetXYZ,
            Tags = [new ProtoTag { Name = "new", StartDay = 0, EndDay = 0 }],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Replace);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(1);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("new");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("existing");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("ore");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("old");
    }

    [Fact]
    public void PasteWithMultipleTags_PreservesAllTags()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags =
            [
                new ProtoTag { Name = "copper", StartDay = 0, EndDay = 0 },
                new ProtoTag { Name = "ore", StartDay = 0, EndDay = 0 },
                new ProtoTag { Name = "rich", StartDay = 0, EndDay = 0 },
                new ProtoTag { Name = "processed", StartDay = 0, EndDay = 0 }
            ],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces[0].Tags.Should().HaveCount(4);
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().BeEquivalentTo("copper", "ore", "rich", "processed");
    }

    [Fact]
    public void PasteWithTemporalTags_PreservesTimeData()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags = [new ProtoTag { Name = "seasonal", StartDay = 10, EndDay = 50 }],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces[0].Tags![0].Name.Should().Be("seasonal");
        playerData.ProtoPlaces[0].Tags![0].StartDay.Should().Be(10);
        playerData.ProtoPlaces[0].Tags![0].EndDay.Should().Be(50);
    }

    [Fact]
    public void PastePreservesExactCoordinates()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(123.456, 78.9, 234.567),
            Tags = [new ProtoTag { Name = "precise", StartDay = 0, EndDay = 0 }],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces[0].XYZ.X.Should().BeApproximately(123.456, 0.001);
        playerData.ProtoPlaces[0].XYZ.Y.Should().BeApproximately(78.9, 0.001);
        playerData.ProtoPlaces[0].XYZ.Z.Should().BeApproximately(234.567, 0.001);
    }

    [Fact]
    public void PasteMultiplePlacesAtSameLocation_MergesByRoughPlace()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place1 = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags = [new ProtoTag { Name = "copper", StartDay = 0, EndDay = 0 }],
        };

        ProtoPlace place2 = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100.5, 50.5, 200.5),
            Tags = [new ProtoTag { Name = "tin", StartDay = 0, EndDay = 0 }],
        };

        helper.SetClipboardWithPlaces(place1, place2);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(1, "places within same rough place should merge");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain(["copper", "tin"]);
    }

    [Fact]
    public void PasteWithSkipAction_AddsNewPlacesOnly()
    {
        TestPlayerData playerData = new();
        Vec3d existingXYZ = playerData.Place(100, 50, 200);
        playerData.AddPlace(existingXYZ, "existing");
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place1 = new()
        {
            XYZ = existingXYZ,
            Tags = [new ProtoTag { Name = "skipped", StartDay = 0, EndDay = 0 }],
        };

        ProtoPlace place2 = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(300, 60, 400),
            Tags = [new ProtoTag { Name = "new", StartDay = 0, EndDay = 0 }],
        };

        helper.SetClipboardWithPlaces(place1, place2);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(2);
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(x => x.Name).Should().Contain("existing");
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(x => x.Name).Should().NotContain("skipped");
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(x => x.Name).Should().Contain("new");
    }

    [Fact]
    public void PasteWithUpdateAction_AddsTagsToAllMatchingPlaces()
    {
        TestPlayerData playerData = new();
        Vec3d xyz1 = playerData.Place(100, 50, 200);
        Vec3d xyz2 = playerData.Place(300, 60, 400);
        playerData.AddPlace(xyz1, "place1");
        playerData.AddPlace(xyz2, "place2");
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place1 = new()
        {
            XYZ = xyz1,
            Tags = [new ProtoTag { Name = "updated1", StartDay = 0, EndDay = 0 }],
        };

        ProtoPlace place2 = new()
        {
            XYZ = xyz2,
            Tags = [new ProtoTag { Name = "updated2", StartDay = 0, EndDay = 0 }],
        };

        helper.SetClipboardWithPlaces(place1, place2);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Update);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(2);
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(x => x.Name).Should().Contain(["place1", "updated1", "place2", "updated2"]);
    }

    [Fact]
    public void PasteWithReplaceAction_ReplacesMultiplePlaces()
    {
        TestPlayerData playerData = new();
        Vec3d xyz1 = playerData.Place(100, 50, 200);
        Vec3d xyz2 = playerData.Place(300, 60, 400);
        playerData.AddPlace(xyz1, "old1");
        playerData.AddPlace(xyz2, "old2");
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place1 = new()
        {
            XYZ = xyz1,
            Tags = [new ProtoTag { Name = "new1", StartDay = 0, EndDay = 0 }],
        };

        ProtoPlace place2 = new()
        {
            XYZ = xyz2,
            Tags = [new ProtoTag { Name = "new2", StartDay = 0, EndDay = 0 }],
        };

        helper.SetClipboardWithPlaces(place1, place2);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Replace);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces.Should().HaveCount(2);
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(x => x.Name).Should().Contain(["new1", "new2"]);
        playerData.ProtoPlaces.SelectMany(p => p.Tags!).Select(x => x.Name).Should().NotContain(["old1", "old2"]);
    }

    [Fact]
    public void PasteWithWhitespaceOnlyClipboard_ReturnsNoClipboardMessage()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("   \t\n  ");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultNoClipboard);
        helper.VerifyPacketNotSent();
    }

    [Fact]
    public void PasteVerifiesActionParameter_Skip()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("[]");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        helper.SentSavePacket!.ExistingPlaceAction.Should().Be(ExistingPlaceAction.Skip);
    }

    [Fact]
    public void PasteVerifiesActionParameter_Update()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("[]");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Update);

        helper.SentSavePacket!.ExistingPlaceAction.Should().Be(ExistingPlaceAction.Update);
    }

    [Fact]
    public void PasteVerifiesActionParameter_Replace()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);
        helper.SetClipboard("[]");

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Replace);

        helper.SentSavePacket!.ExistingPlaceAction.Should().Be(ExistingPlaceAction.Replace);
    }

    [Fact]
    public void PasteWithExpiredTemporalTags_FiltersOutExpiredTags()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags =
            [
                new ProtoTag { Name = "expired", StartDay = 0, EndDay = 1 },
                new ProtoTag { Name = "current", StartDay = 0, EndDay = 100 }
            ],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain("current");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().NotContain("expired", "expired tags should be filtered out");
    }

    [Fact]
    public void PasteWithDuplicateTagsAtSamePlace_DeduplicatesTags()
    {
        TestPlayerData playerData = new();
        PastePlacesTestHelper helper = new(playerData);

        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(100, 50, 200),
            Tags =
            [
                new ProtoTag { Name = "copper", StartDay = 0, EndDay = 0 },
                new ProtoTag { Name = "copper", StartDay = 0, EndDay = 0 },
                new ProtoTag { Name = "ore", StartDay = 0, EndDay = 0 }
            ],
        };
        helper.SetClipboardWithPlaces(place);

        LocalizedTextCommandResult commandResult = helper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        playerData.ProtoPlaces[0].Tags!.Where(x => x.Name == "copper").Should().HaveCount(1, "duplicate tags should be removed");
        playerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain(["copper", "ore"]);
    }

    [Fact]
    public void PasteCopyRoundtrip_PreservesAllData()
    {
        TestPlayerData originalPlayerData = new();
        originalPlayerData.AddPlace(originalPlayerData.Place(100, 50, 200), "copper", "ore");
        originalPlayerData.AddPlace(originalPlayerData.Place(300, 60, 400), "tin");

        CopyPlacesTestHelper copyHelper = new(originalPlayerData);
        copyHelper.ExecuteCopyCommand(int.MaxValue, "");

        TestPlayerData newPlayerData = new();
        PastePlacesTestHelper pasteHelper = new(newPlayerData);
        pasteHelper.SetClipboard(copyHelper.ClipboardText!);

        LocalizedTextCommandResult pasteResult = pasteHelper.ExecutePasteCommand(ExistingPlaceAction.Skip);

        pasteResult.AsSuccess.Key.Should().Be(LocalizedTexts.pasteInterestingPlacesResultUploadInProgress);

        newPlayerData.ProtoPlaces.Should().HaveCount(2);
        newPlayerData.ProtoPlaces[0].Tags!.Select(x => x.Name).Should().Contain(["copper", "ore"]);
        newPlayerData.ProtoPlaces[1].Tags!.Select(x => x.Name).Should().Contain("tin");
    }
}
