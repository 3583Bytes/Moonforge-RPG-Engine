using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class BestiaryStateSnapshot
    {
        public List<BestiaryEntrySnapshot> Entries { get; set; } = new();
    }

    public sealed class BestiaryEntrySnapshot
    {
        public string SpeciesId { get; set; } = string.Empty;

        public int EncounterCount { get; set; }

        public int CaptureCount { get; set; }

        public long? FirstEncounteredAtMinutes { get; set; }

        public long? FirstCapturedAtMinutes { get; set; }
    }
}
