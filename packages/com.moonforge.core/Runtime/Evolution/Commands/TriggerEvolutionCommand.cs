using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Evolution.Commands
{

    /// <summary>
    /// Manually fires an evolution for an actor. Use for non-LevelUp triggers (item use, scripted
    /// story events). The handler validates that the actor is eligible (registered via
    /// <see cref="ConfigureActorEvolutionsCommand"/>) and that the trigger conditions are met,
    /// then emits <see cref="Events.EvolutionTriggeredEvent"/>.
    ///
    /// LevelUp-trigger evolutions are fired automatically by <see cref="Reactors.LevelUpEvolutionReactor"/>;
    /// calling this command for them is still valid but typically unnecessary.
    /// </summary>
    public sealed class TriggerEvolutionCommand : ICommand
    {
        public TriggerEvolutionCommand(string actorId, string evolutionId)
        {
            ActorId = actorId;
            EvolutionId = evolutionId;
        }

        public string ActorId { get; }

        public string EvolutionId { get; }
    }
}
