﻿using Domain.Common.Seedwork.Abstract;

namespace Domain.Aggregates.Bus;

public class Bus : Aggregate<Bus>
{
    public Bus(string id, string name, string tripId, int currentStopIndex)
    {
        Id = id;
        Name = name;
        TripId = tripId;
        CurrentStopIndex = currentStopIndex;
    }

    public string Name { get; internal set; }

    public string TripId { get; internal set; }

    public int CurrentStopIndex { get; internal set; }
}