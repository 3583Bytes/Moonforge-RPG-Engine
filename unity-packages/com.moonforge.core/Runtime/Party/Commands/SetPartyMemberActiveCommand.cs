using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Party.Commands
{

    public sealed class SetPartyMemberActiveCommand : ICommand
    {
        public SetPartyMemberActiveCommand(string actorId, bool active)
        {
            ActorId = actorId;
            Active = active;
        }

        public string ActorId { get; }

        public bool Active { get; }
    }
}
