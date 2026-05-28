using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Quests.Commands
{

    public sealed class ClaimQuestRewardsCommand : ICommand
    {
        public ClaimQuestRewardsCommand(string questId)
        {
            QuestId = questId;
        }

        public string QuestId { get; }
    }
}
