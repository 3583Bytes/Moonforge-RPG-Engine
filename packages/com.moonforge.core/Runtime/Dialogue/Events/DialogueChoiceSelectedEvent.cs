using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Dialogue.Events;

public sealed class DialogueChoiceSelectedEvent : DomainEvent
{
    public DialogueChoiceSelectedEvent(string dialogueId, string nodeId, string choiceId)
        : base(nameof(DialogueChoiceSelectedEvent))
    {
        DialogueId = dialogueId;
        NodeId = nodeId;
        ChoiceId = choiceId;
    }

    public string DialogueId { get; }

    public string NodeId { get; }

    public string ChoiceId { get; }
}
