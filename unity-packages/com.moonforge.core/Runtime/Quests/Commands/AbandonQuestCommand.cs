using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Quests.Commands
{

    public sealed class AbandonQuestCommand : ICommand
    {
        public AbandonQuestCommand(string questId)
        {
            QuestId = questId;
        }

        public string QuestId { get; }
    }
}
