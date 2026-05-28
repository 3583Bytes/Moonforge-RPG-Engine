namespace Moonforge.Core.Runtime.Time
{

    public interface IGameClock
    {
        long CurrentSimulationMinutes { get; }

        void AdvanceMinutes(long minutes);
    }
}
