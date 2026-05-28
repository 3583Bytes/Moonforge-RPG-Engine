using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Dialogue.Commands
{

    public sealed class ChooseDialogueChoiceCommand : ICommand
    {
        public ChooseDialogueChoiceCommand(string dialogueId, string choiceId)
        {
            DialogueId = dialogueId;
            ChoiceId = choiceId;
        }

        public string DialogueId { get; }

        public string ChoiceId { get; }
    }
}
