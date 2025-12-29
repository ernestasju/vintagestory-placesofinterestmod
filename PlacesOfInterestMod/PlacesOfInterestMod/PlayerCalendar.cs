namespace PlacesOfInterestMod;

public sealed class PlayerCalendar
{
    private readonly IPlayerPlacesOfInterest _poi;

    public PlayerCalendar(IPlayerPlacesOfInterest poi)
    {
        _poi = poi;
    }

    public int Today => (int)_poi.Player.Calendar.TotalDays;

    public int CalculateDay(int offset, PeriodUnit unit)
    {
        return Today + (offset * NumberOfDays(unit));
    }

    public int NumberOfDays(PeriodUnit unit)
    {
        return unit switch
        {
            PeriodUnit.Day => 1,
            PeriodUnit.Month => _poi.Player.Calendar.DaysPerMonth,
            PeriodUnit.Quarter => _poi.Player.Calendar.DaysPerMonth * 4,
            PeriodUnit.Year => _poi.Player.Calendar.DaysPerYear,
            PeriodUnit.ResinWeek => 7,
            _ => 0,
        };
    }
}
