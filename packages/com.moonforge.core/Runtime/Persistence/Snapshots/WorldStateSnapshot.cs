using System.Collections.Generic;
using Moonforge.Core.World;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class WorldStateSnapshot
    {
        public List<WorldVariableSnapshot> Variables { get; set; } = new();
    }

    public sealed class WorldVariableSnapshot
    {
        public string Key { get; set; } = string.Empty;

        public WorldVariableKind Kind { get; set; }

        public bool BoolValue { get; set; }

        public int IntValue { get; set; }

        public double FloatValue { get; set; }

        public string StringValue { get; set; } = string.Empty;
    }
}
