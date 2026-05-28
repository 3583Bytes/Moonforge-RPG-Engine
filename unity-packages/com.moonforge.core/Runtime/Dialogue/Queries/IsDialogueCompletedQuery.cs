using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Dialogue.Queries
{

    public sealed class IsDialogueCompletedQuery : IQuery<bool>
    {
        public IsDialogueCompletedQuery(string dialogueId)
        {
            DialogueId = dialogueId;
        }

        public string DialogueId { get; }
    }
}
