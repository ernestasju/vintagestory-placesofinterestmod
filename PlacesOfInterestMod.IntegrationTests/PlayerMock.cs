using Moq;
using Vintagestory.API.Common;

namespace PlacesOfInterestMod.IntegrationTests;

/// <summary>
/// Mock for Vintage Story player API.
/// </summary>
/// <remarks>
/// Create a new mock instance for each chat command call.
/// </remarks>
public sealed class PlayerMock
{
    private readonly TestPlayerData _player;
    private readonly Mock<IGameCalendar> _gameCalendarMock;
    private readonly Mock<IVintageStoryPlayer> _playerMock;

    public PlayerMock(TestPlayerData player)
    {
        _player = player;

        _gameCalendarMock = new();
        _gameCalendarMock.Setup(x => x.TotalDays).Returns(44);
        _gameCalendarMock.Setup(x => x.DaysPerMonth).Returns(9);
        _gameCalendarMock.Setup(x => x.DaysPerYear).Returns(9 * 12);

        _playerMock = new();
        _playerMock.Setup(x => x.Calendar).Returns(_gameCalendarMock.Object);
        _playerMock.Setup(x => x.XYZ).Returns(_player.XYZ);
        _playerMock
            .Setup(x => x.LoadModData(It.IsAny<string>(), It.IsAny<List<ProtoPlace>>()))
            .Returns(_player.ProtoPlaces);
        _playerMock
            .Setup(x => x.SaveModData(It.IsAny<string>(), It.IsAny<List<ProtoPlace>>()))
            .Callback(
                (string _, List<ProtoPlace> xs) =>
                {
                    _player.ProtoPlaces.Clear();
                    _player.ProtoPlaces.AddRange(xs);
                });
        _playerMock
            .Setup(x => x.RemoveModData(It.IsAny<string>()))
            .Callback((string _) => _player.ProtoPlaces.Clear());
    }

    public IVintageStoryPlayer Object => _playerMock.Object;
}
