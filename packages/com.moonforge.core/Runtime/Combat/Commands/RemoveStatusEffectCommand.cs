using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands;

public sealed class RemoveStatusEffectCommand : ICommand
{
    public RemoveStatusEffectCommand(string actorId, string statusId)
    {
        ActorId = actorId;
        StatusId = statusId;
    }

    public string ActorId { get; }

    public string StatusId { get; }
}
