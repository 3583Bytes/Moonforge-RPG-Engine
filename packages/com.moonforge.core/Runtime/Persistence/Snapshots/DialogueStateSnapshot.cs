using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots;

public sealed class DialogueStateSnapshot
{
    public List<DialogueInstanceSnapshot> Dialogues { get; set; } = new();
}

public sealed class DialogueInstanceSnapshot
{
    public string DialogueId { get; set; } = string.Empty;

    public string? CurrentNodeId { get; set; }

    public bool Completed { get; set; }

    public List<string> VisitedNodes { get; set; } = new();

    public List<string> ChosenChoices { get; set; } = new();
}
