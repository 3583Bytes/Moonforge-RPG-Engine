using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Dialogue.Events;

public sealed class DialogueStartedEvent : DomainEvent
{
    public DialogueStartedEvent(string dialogueId)
        : base(nameof(DialogueStartedEvent))
    {
        DialogueId = dialogueId;
    }

    public string DialogueId { get; }
}
