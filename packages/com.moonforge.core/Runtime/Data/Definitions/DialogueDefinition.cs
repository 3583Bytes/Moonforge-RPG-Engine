using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class DialogueDefinition
    {
        public DialogueDefinition(
            string id,
            string startNodeId,
            IReadOnlyList<DialogueNodeDefinition> nodes)
        {
            Id = id;
            StartNodeId = startNodeId;
            Nodes = nodes ?? System.Array.Empty<DialogueNodeDefinition>();
        }

        public string Id { get; }

        public string StartNodeId { get; }

        public IReadOnlyList<DialogueNodeDefinition> Nodes { get; }
    }
}
