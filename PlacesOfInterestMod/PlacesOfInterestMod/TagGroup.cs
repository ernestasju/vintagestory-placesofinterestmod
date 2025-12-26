namespace PlacesOfInterestMod;

public sealed class TagGroup
{
    public required TagName[] Names { get; init; }

    public required int StartDay { get; init; }

    public required int EndDay { get; init; }
}
