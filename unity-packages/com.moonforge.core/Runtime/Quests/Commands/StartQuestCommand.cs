using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Quests.Commands
{

    public sealed class StartQuestCommand : ICommand
    {
        public StartQuestCommand(string questId)
        {
            QuestId = questId;
        }

        public string QuestId { get; }
    }
}
