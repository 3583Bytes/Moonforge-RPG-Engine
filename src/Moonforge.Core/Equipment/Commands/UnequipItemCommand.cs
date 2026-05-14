using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Equipment.Commands;

public sealed class UnequipItemCommand : ICommand
{
    public UnequipItemCommand(string slotId)
        : this(slotId, EquipItemCommand.DefaultActorId)
    {
    }

    public UnequipItemCommand(string slotId, string actorId)
    {
        SlotId = slotId;
        ActorId = string.IsNullOrWhiteSpace(actorId) ? EquipItemCommand.DefaultActorId : actorId;
    }

    public string SlotId { get; }

    public string ActorId { get; }
}
