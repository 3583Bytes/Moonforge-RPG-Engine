using System;

namespace Moonforge.Core.Runtime.Time;

public sealed class SimulationClock : IGameClock
{
    private long _currentSimulationMinutes;

    public SimulationClock(long initialSimulationMinutes = 0)
    {
        _currentSimulationMinutes = initialSimulationMinutes;
    }

    public long CurrentSimulationMinutes => _currentSimulationMinutes;

    public void AdvanceMinutes(long minutes)
    {
        if (minutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes));
        }

        _currentSimulationMinutes += minutes;
    }
}
