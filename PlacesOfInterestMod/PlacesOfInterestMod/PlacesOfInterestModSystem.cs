using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlacesOfInterestMod;

public class PlacesOfInterestModSystem : ModSystem
{
    public const string PlacesOfInterestNetworkChannelName = "places-of-interest-mod";

    // NOTE: These fields are used to keep references to the server-side and client-side instances for entire lifetime of the mod system.
#pragma warning disable S1450 // Private fields only used as local variables in methods should become local variables
    private ServerSide? _serverSide;
    private ClientSide? _clientSide;
#pragma warning restore S1450 // Private fields only used as local variables in methods should become local variables

    public override void Start(ICoreAPI api)
    {
        Mod.Logger.Notification("Hello from Places of Interest mod: " + api.Side);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        Mod.Logger.Notification("Hello from Places of Interest server side: " + Lang.Get(LocalizedTexts.hello));

        _serverSide = new(api, api.Network.RegisterChannel(PlacesOfInterestNetworkChannelName));
        _serverSide.Register();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        Mod.Logger.Notification("Hello from Places of Interest client side: " + Lang.Get(LocalizedTexts.hello));

        _clientSide = new(api, api.Network.RegisterChannel(PlacesOfInterestNetworkChannelName));
        _clientSide.Register();
    }
}
