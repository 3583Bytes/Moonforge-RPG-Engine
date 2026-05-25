using System.Collections.Generic;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Dialogue.Queries;

public sealed class GetAvailableDialogueChoicesQuery : IQuery<IReadOnlyList<string>>
{
    public GetAvailableDialogueChoicesQuery(string dialogueId)
    {
        DialogueId = dialogueId;
    }

    public string DialogueId { get; }
}
