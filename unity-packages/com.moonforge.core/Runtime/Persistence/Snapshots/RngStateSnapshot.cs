namespace Moonforge.Core.Persistence.Snapshots
{

    /// <summary>
    /// Serialized PCG32 stream position so deterministic random draws resume exactly where
    /// they left off after a save/load cycle. Optional — legacy saves (schema &lt; 8) and
    /// hosts that re-derive seeds per operation leave it null.
    /// </summary>
    public sealed class RngStateSnapshot
    {
        public ulong State { get; set; }

        public ulong Increment { get; set; }
    }
}
