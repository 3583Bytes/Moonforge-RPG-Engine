using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Dialogue.Queries;

public sealed class GetDialogueCurrentNodeQuery : IQuery<string?>
{
    public GetDialogueCurrentNodeQuery(string dialogueId)
    {
        DialogueId = dialogueId;
    }

    public string DialogueId { get; }
}
