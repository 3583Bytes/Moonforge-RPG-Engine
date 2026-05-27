using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Progression.Commands
{

    public sealed class GrantExperienceCommand : ICommand
    {
        public GrantExperienceCommand(string actorId, long amount)
        {
            ActorId = actorId;
            Amount = amount;
        }

        public string ActorId { get; }

        public long Amount { get; }
    }
}
