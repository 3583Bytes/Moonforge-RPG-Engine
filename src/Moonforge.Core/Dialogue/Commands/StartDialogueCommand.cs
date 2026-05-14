using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Dialogue.Commands;

public sealed class StartDialogueCommand : ICommand
{
    public StartDialogueCommand(string dialogueId)
    {
        DialogueId = dialogueId;
    }

    public string DialogueId { get; }
}
