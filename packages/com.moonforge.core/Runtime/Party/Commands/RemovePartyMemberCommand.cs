using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Party.Commands;

public sealed class RemovePartyMemberCommand : ICommand
{
    public RemovePartyMemberCommand(string actorId)
    {
        ActorId = actorId;
    }

    public string ActorId { get; }
}
