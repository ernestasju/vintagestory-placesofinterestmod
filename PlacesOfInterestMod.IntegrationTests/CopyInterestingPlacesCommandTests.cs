using FluentAssertions;
using Moq;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Helper class to simulate client-server communication for .copyInterestingPlaces command.
/// </summary>
public sealed class CopyPlacesTestHelper
{
    private readonly TestPlayerData _playerData;
    private readonly Mock<IClientSide> _clientSideMock;
    private readonly Mock<IVintageStoryServerPlayer> _serverPlayerMock;
    private LoadPlacesPacket? _sentLoadPacket;
    private LoadedPlacesPacket? _receivedLoadedPacket;
    private string? _clipboardText;
    private LocalizedText? _chatMessage;

    public CopyPlacesTestHelper(TestPlayerData playerData)
    {
        _playerData = playerData;
        _clientSideMock = new Mock<IClientSide>();
        _serverPlayerMock = new Mock<IVintageStoryServerPlayer>();

        SetupClientSide();
        SetupServerSide();
    }

    public TestPlayerData PlayerData => _playerData;

    public LoadPlacesPacket? SentLoadPacket => _sentLoadPacket;

    public LoadedPlacesPacket? ReceivedLoadedPacket => _receivedLoadedPacket;

    public string? ClipboardText => _clipboardText;

    public LocalizedText? ChatMessage => _chatMessage;

    private void SetupClientSide()
    {
        _clientSideMock
            .Setup(x => x.SendNetworkPacketToServerSide(It.IsAny<LoadPlacesPacket>()))
            .Callback<LoadPlacesPacket>(packet =>
            {
                _sentLoadPacket = packet;
                SimulateServerProcessing(packet);
            });

        _clientSideMock
            .Setup(x => x.SetClipboardText(It.IsAny<string>()))
            .Callback<string>(text => _clipboardText = text);

        _clientSideMock
            .Setup(x => x.TriggerChatMessage(It.IsAny<LocalizedText>()))
            .Callback<LocalizedText>(msg => _chatMessage = msg);
    }

    private void SetupServerSide()
    {
        PlayerMock playerMock = new(_playerData);
        _serverPlayerMock.Setup(x => x.Player).Returns(playerMock.Object);

        _serverPlayerMock
            .Setup(x => x.SendPacket(It.IsAny<LoadedPlacesPacket>()))
            .Callback<LoadedPlacesPacket>(packet =>
            {
                _receivedLoadedPacket = packet;
                SimulateClientReceivingPacket(packet);
            });
    }

    private void SimulateServerProcessing(LoadPlacesPacket packet)
    {
        ServerSide.HandleNetworkPacket(_serverPlayerMock.Object, packet);
    }

    private void SimulateClientReceivingPacket(LoadedPlacesPacket packet)
    {
        ClientSide.HandleNetworkPacket(_clientSideMock.Object, packet);
    }

    public LocalizedTextCommandResult ExecuteCopyCommand(int radius, string tags)
    {
        return ClientSide.HandleChatCommandCopyInterestingPlaces(_clientSideMock.Object, radius, tags);
    }

    public void VerifyPacketSent()
    {
        _clientSideMock.Verify(
            x => x.SendNetworkPacketToServerSide(It.IsAny<LoadPlacesPacket>()),
            Times.Once);
    }

    public void VerifyClipboardSet()
    {
        _clientSideMock.Verify(x => x.SetClipboardText(It.IsAny<string>()), Times.Once);
    }

    public void VerifyChatMessageSent()
    {
        _clientSideMock.Verify(x => x.TriggerChatMessage(It.IsAny<LocalizedText>()), Times.Once);
    }
}

/// <summary>
/// Tests for the .copyInterestingPlaces command (client-side command with server-side processing).
/// </summary>
/// <remarks>
/// Flow:
/// 1. Client executes command and sends LoadPlacesPacket to server
/// 2. Server processes packet, queries places, sends LoadedPlacesPacket back
/// 3. Client receives packet, serializes to JSON, sets clipboard, triggers chat message
/// </remarks>
public class CopyInterestingPlacesCommandTests
{
    [Fact]
    public void CopyWithNoPlaces_CompletesFullFlow()
    {
        TestPlayerData playerData = new();
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.SentLoadPacket.Should().NotBeNull();
        helper.SentLoadPacket!.SearchRadius.Should().Be(100);
        helper.SentLoadPacket.TagQueriesText.Should().Be("");

        helper.ReceivedLoadedPacket.Should().NotBeNull();
        helper.ReceivedLoadedPacket!.Places.Should().BeEmpty();

        helper.ClipboardText.Should().Be("[]");

        helper.ChatMessage.Should().NotBeNull();
        helper.ChatMessage!.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResult);
        helper.ChatMessage.Args[0].Should().Be(0);

        helper.VerifyPacketSent();
        helper.VerifyClipboardSet();
        helper.VerifyChatMessageSent();
    }

    [Fact]
    public void CopyWithSinglePlace_SerializesPlaceToClipboard()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "copper");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("copper");

        helper.ClipboardText.Should().NotBeNullOrEmpty();
        helper.ClipboardText.Should().Contain("copper");

        helper.ChatMessage!.Args[0].Should().Be(1);
    }

    [Fact]
    public void CopyWithMultiplePlaces_SerializesAllPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(20, 0, 0), "tin");
        playerData.AddPlace(playerData.Place(30, 0, 0), "gold");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(3);

        helper.ClipboardText.Should().Contain("copper");
        helper.ClipboardText.Should().Contain("tin");
        helper.ClipboardText.Should().Contain("gold");

        helper.ChatMessage!.Args[0].Should().Be(3);
    }

    [Fact]
    public void CopyWithRadiusFilter_OnlyCopiesPlacesWithinRadius()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "near");
        playerData.AddPlace(playerData.Place(150, 0, 0), "far");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(50, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("near");

        helper.ClipboardText.Should().Contain("near");
        helper.ClipboardText.Should().NotContain("far");

        helper.ChatMessage!.Args[0].Should().Be(1);
    }

    [Fact]
    public void CopyWithTagFilter_OnlyCopiesMatchingPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(20, 0, 0), "tin", "ore");
        playerData.AddPlace(playerData.Place(30, 0, 0), "village");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(2);

        helper.ClipboardText.Should().Contain("copper");
        helper.ClipboardText.Should().Contain("tin");
        helper.ClipboardText.Should().NotContain("village");

        helper.ChatMessage!.Args[0].Should().Be(2);
    }

    [Fact]
    public void CopyWithExclusionFilter_ExcludesMatchingPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper", "depleted");
        playerData.AddPlace(playerData.Place(20, 0, 0), "copper");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "copper -depleted");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().NotContain("depleted");

        helper.ClipboardText.Should().NotContain("depleted");

        helper.ChatMessage!.Args[0].Should().Be(1);
    }

    [Fact]
    public void CopyWithComplexTagQuery_FiltersCorrectly()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper", "ore", "rich");
        playerData.AddPlace(playerData.Place(20, 0, 0), "copper", "ore");
        playerData.AddPlace(playerData.Place(30, 0, 0), "tin", "ore");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "copper ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(2);
        helper.ReceivedLoadedPacket.Places.Should().AllSatisfy(p =>
            p.Tags!.Select(t => t.Name).Should().Contain("copper"));

        helper.ChatMessage!.Args[0].Should().Be(2);
    }

    [Fact]
    public void CopyWithWildcardPattern_MatchesPattern()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper_ore");
        playerData.AddPlace(playerData.Place(20, 0, 0), "tin_ore");
        playerData.AddPlace(playerData.Place(30, 0, 0), "coal");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "~*ore");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(2);

        helper.ClipboardText.Should().Contain("copper_ore");
        helper.ClipboardText.Should().Contain("tin_ore");
        helper.ClipboardText.Should().NotContain("coal");

        helper.ChatMessage!.Args[0].Should().Be(2);
    }

    [Fact]
    public void CopyWithZeroRadius_UsesDefaultBehavior()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "home");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(0, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.SentLoadPacket!.SearchRadius.Should().Be(0);
    }

    [Fact]
    public void CopyWithLargeRadius_CopiesDistantPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10000, 0, 0), "faraway");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(int.MaxValue, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ClipboardText.Should().Contain("faraway");
        helper.ChatMessage!.Args[0].Should().Be(1);
    }

    [Fact]
    public void CopyWithMultipleTags_PreservesAllTags()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper", "ore", "rich", "processed");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places[0].Tags.Should().HaveCount(4);

        helper.ClipboardText.Should().Contain("copper");
        helper.ClipboardText.Should().Contain("ore");
        helper.ClipboardText.Should().Contain("rich");
        helper.ClipboardText.Should().Contain("processed");
    }

    [Fact]
    public void CopyWithTemporalTags_PreservesTimeData()
    {
        TestPlayerData playerData = new();
        ProtoPlace place = new()
        {
            XYZ = playerData.XYZ,
            Tags = [new ProtoTag { Name = "seasonal", StartDay = 10, EndDay = 50 }],
        };
        playerData.ProtoPlaces.Add(place);
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places[0].Tags![0].StartDay.Should().Be(10);
        helper.ReceivedLoadedPacket.Places[0].Tags![0].EndDay.Should().Be(50);

        helper.ClipboardText.Should().Contain("seasonal");
        helper.ClipboardText.Should().Contain("10");
        helper.ClipboardText.Should().Contain("50");
    }

    [Fact]
    public void CopyPlacesAtExactRadius_IncludesPlacesOnBoundary()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(50, 0, 0), "boundary");
        playerData.AddPlace(playerData.Place(51, 0, 0), "outside");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(50, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("boundary");
    }

    [Fact]
    public void CopyWithEmptyTagQuery_CopiesAllNonExcludedPlaces()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper");
        playerData.AddPlace(playerData.Place(20, 0, 0), "tin");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(2);
        helper.ChatMessage!.Args[0].Should().Be(2);
    }

    [Fact]
    public void CopyPreservesExactCoordinates()
    {
        TestPlayerData playerData = new();
        ProtoPlace place = new()
        {
            XYZ = new Vintagestory.API.MathTools.Vec3d(123.456, 78.9, 234.567),
            Tags = [new ProtoTag { Name = "precise", StartDay = 0, EndDay = 0 }],
        };
        playerData.ProtoPlaces.Add(place);
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(int.MaxValue, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ClipboardText.Should().Contain("123.456");
        helper.ClipboardText.Should().Contain("78.9");
        helper.ClipboardText.Should().Contain("234.567");
    }

    [Fact]
    public void CopyWithRadiusAndTagFilter_AppliesBothFilters()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "copper", "near");
        playerData.AddPlace(playerData.Place(20, 0, 0), "tin", "near");
        playerData.AddPlace(playerData.Place(150, 0, 0), "copper", "far");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(50, "copper");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("copper");
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("near");

        helper.ChatMessage!.Args[0].Should().Be(1);
    }

    [Fact]
    public void CopyVerifiesJsonArrayFormat()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.Place(10, 0, 0), "test");
        CopyPlacesTestHelper helper = new(playerData);

        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(100, "");

        commandResult.AsSuccess.Key.Should().Be(LocalizedTexts.copyInterestingPlacesResultDownloadInProgress);

        helper.ClipboardText.Should().StartWith("[");
        helper.ClipboardText.Should().EndWith("]");
    }

    [Fact]
    public void CopyWithPlayerMovement_CopiesRelativeToCurrentPosition()
    {
        TestPlayerData playerData = new();
        playerData.AddPlace(playerData.XYZ, "start");
        playerData.AddPlace(playerData.Place(100, 0, 0), "destination");

        CopyPlacesTestHelper helper = new(playerData);
        LocalizedTextCommandResult commandResult = helper.ExecuteCopyCommand(50, "");

        helper.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("start");

        playerData.MoveToDifferentPlace(100, 0, 0);
        CopyPlacesTestHelper helper2 = new(playerData);
        LocalizedTextCommandResult commandResult2 = helper2.ExecuteCopyCommand(50, "");

        helper2.ReceivedLoadedPacket!.Places.Should().HaveCount(1);
        helper2.ReceivedLoadedPacket.Places[0].Tags!.Select(x => x.Name).Should().Contain("destination");
    }
}
