using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class PartyStateSnapshot
    {
        public int MaxActive { get; set; } = 1;

        public int MaxRoster { get; set; } = 1;

        public List<PartyMemberSnapshot> Members { get; set; } = new();
    }

    public sealed class PartyMemberSnapshot
    {
        public string ActorId { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
