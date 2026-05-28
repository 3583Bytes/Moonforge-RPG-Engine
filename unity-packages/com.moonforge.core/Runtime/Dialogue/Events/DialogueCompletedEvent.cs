using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Dialogue.Events
{

    public sealed class DialogueCompletedEvent : DomainEvent
    {
        public DialogueCompletedEvent(string dialogueId)
            : base(nameof(DialogueCompletedEvent))
        {
            DialogueId = dialogueId;
        }

        public string DialogueId { get; }
    }
}
