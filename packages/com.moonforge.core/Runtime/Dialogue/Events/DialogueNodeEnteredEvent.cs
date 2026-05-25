using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Dialogue.Events;

public sealed class DialogueNodeEnteredEvent : DomainEvent
{
    public DialogueNodeEnteredEvent(string dialogueId, string nodeId)
        : base(nameof(DialogueNodeEnteredEvent))
    {
        DialogueId = dialogueId;
        NodeId = nodeId;
    }

    public string DialogueId { get; }

    public string NodeId { get; }
}
