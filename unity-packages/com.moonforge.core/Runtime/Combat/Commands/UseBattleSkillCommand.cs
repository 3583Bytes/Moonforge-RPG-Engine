using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands
{

    public sealed class UseBattleSkillCommand : ICommand
    {
        public UseBattleSkillCommand(string actorId, string skillId, string targetActorId)
        {
            ActorId = actorId;
            SkillId = skillId;
            TargetActorId = targetActorId;
        }

        public string ActorId { get; }

        public string SkillId { get; }

        public string TargetActorId { get; }
    }
}
