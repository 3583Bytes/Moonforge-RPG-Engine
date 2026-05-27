using System.Collections.Generic;
using Moonforge.Core.Interactables;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class InteractablesStateSnapshot
    {
        public List<InteractableInstanceSnapshot> Instances { get; set; } = new();
    }

    public sealed class InteractableInstanceSnapshot
    {
        public string InstanceId { get; set; } = string.Empty;

        public string DefinitionId { get; set; } = string.Empty;

        public int X { get; set; }

        public int Y { get; set; }

        public InteractableStatus Status { get; set; }

        public int UsesRemaining { get; set; }

        public bool Locked { get; set; }
    }
}
