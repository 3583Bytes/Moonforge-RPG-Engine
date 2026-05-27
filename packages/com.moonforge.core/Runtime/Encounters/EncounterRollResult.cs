using System.Collections.Generic;

namespace Moonforge.Core.Encounters
{

    public sealed class EncounterRollResult
    {
        public EncounterRollResult(IReadOnlyList<EncounterSpawn> spawns)
        {
            Spawns = spawns;
        }

        public IReadOnlyList<EncounterSpawn> Spawns { get; }

        public bool IsEmpty => Spawns.Count == 0;

        public static readonly EncounterRollResult Empty = new(System.Array.Empty<EncounterSpawn>());
    }
}
