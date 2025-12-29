namespace PlacesOfInterestMod;

public interface IVintageStoryServerPlayer
{
    IVintageStoryPlayer Player { get; }

    void SendPacket<TPacket>(TPacket packet);
}