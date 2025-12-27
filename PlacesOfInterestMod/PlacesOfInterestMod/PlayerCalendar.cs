using System;
using Vintagestory.API.Common;

namespace PlacesOfInterestMod;

public sealed class PlayerCalendar
{
    private readonly PlayerPlacesOfInterest _poi;

    public PlayerCalendar(PlayerPlacesOfInterest poi)
    {
        _poi = poi;
    }

    public int Today => (int)_poi.Player.Entity.World.Calendar.TotalDays;

    public int CalculateDay(int offset, PeriodUnit unit)
    {
        return Today + (offset * NumberOfDays(unit));
    }

    public int NumberOfDays(PeriodUnit unit)
    {
        return unit switch
        {
            PeriodUnit.Day => 1,
            PeriodUnit.Month => _poi.Player.Entity.World.Calendar.DaysPerMonth,
            PeriodUnit.Quarter => _poi.Player.Entity.World.Calendar.DaysPerMonth * 4,
            PeriodUnit.Year => _poi.Player.Entity.World.Calendar.DaysPerYear,
            PeriodUnit.ResinWeek => 7,
            _ => 0,
        };
    }
}
