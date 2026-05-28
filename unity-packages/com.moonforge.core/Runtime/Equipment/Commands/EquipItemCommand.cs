using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Equipment.Commands
{

    public sealed class EquipItemCommand : ICommand
    {
        public const string DefaultActorId = "player";

        public EquipItemCommand(string itemId)
            : this(itemId, DefaultActorId)
        {
        }

        public EquipItemCommand(string itemId, string actorId)
        {
            ItemId = itemId;
            ActorId = string.IsNullOrWhiteSpace(actorId) ? DefaultActorId : actorId;
        }

        public string ItemId { get; }

        public string ActorId { get; }
    }
}
