namespace PlacesOfInterestMod;

public sealed class Tag
{
    public required TagName Name { get; init; }

    public required int StartDay { get; init; }

    public required int EndDay { get; init; }

    public bool IsActive(int day)
    {
        if (StartDay >= 0 && day < StartDay)
        {
            return false;
        }

        if (EndDay > 0 && day > EndDay)
        {
            return false;
        }

        return true;
    }
}