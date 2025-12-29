using System;
using Vintagestory.API.Server;

namespace PlacesOfInterestMod;

public sealed class VintageStoryServerPlayer : IVintageStoryServerPlayer
{
    private readonly IServerPlayer _player;
    private readonly IServerNetworkChannel _networkChannel;

    public VintageStoryServerPlayer(
        IServerPlayer player,
        IServerNetworkChannel networkChannel)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(networkChannel);

        _player = player;
        _networkChannel = networkChannel;
    }

    public IVintageStoryPlayer Player => new VintageStoryPlayer(_player);

    public void SendPacket<TPacket>(TPacket packet)
    {
        _networkChannel.SendPacket(packet, _player);
    }
}
