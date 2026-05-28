using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Party.Commands
{

    public sealed class AddPartyMemberCommand : ICommand
    {
        public AddPartyMemberCommand(string actorId, bool active = true)
        {
            ActorId = actorId;
            Active = active;
        }

        public string ActorId { get; }

        public bool Active { get; }
    }
}
