namespace PlacesOfInterestMod;

public interface IClientSide
{
    string? GetClipboardText();

    void SetClipboardText(string text);

    void TriggerChatMessage(LocalizedText localizedText);

    void SendNetworkPacketToServerSide<T>(T packet);
}