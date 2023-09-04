﻿using Domain.Common.Seedwork.Abstract;

namespace Domain.Aggregates.Trip;

public class ScheduledStop : Entity<ScheduledStop>
{
    public ScheduledStop(string id, string stopId, DateTime departureTime)
    {
        Id = id;
        StopId = stopId;
        DepartureTime = departureTime;
    }

    public string StopId { get; internal set; }

    public DateTime DepartureTime { get; internal set; }

    internal bool UpdateDepartureTime(DateTime departureTime)
    {
        if (DepartureTime.Equals(departureTime)) return false;

        DepartureTime = departureTime;

        return true;
    }
}