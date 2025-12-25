namespace PlacesOfInterestMod;

public sealed class TagGroup
{
    public required string[] Names { get; init; }

    public required int StartDay { get; init; }

    public required int EndDay { get; init; }
}
