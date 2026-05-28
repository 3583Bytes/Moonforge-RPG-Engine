using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Evolution.Events
{

    /// <summary>
    /// Raised when an evolution trigger fires (either from <see cref="Reactors.LevelUpEvolutionReactor"/>
    /// or via <see cref="Commands.TriggerEvolutionCommand"/>). The engine does not apply the
    /// outcome — the game listens for this event and dispatches whatever follow-up commands are
    /// needed (stat changes, type updates, display-name change, species swap).
    /// </summary>
    public sealed class EvolutionTriggeredEvent : DomainEvent
    {
        public EvolutionTriggeredEvent(string actorId, string evolutionId, EvolutionTrigger trigger, string? evolvedSpeciesId)
            : base(nameof(EvolutionTriggeredEvent))
        {
            ActorId = actorId;
            EvolutionId = evolutionId;
            Trigger = trigger;
            EvolvedSpeciesId = evolvedSpeciesId;
        }

        public string ActorId { get; }

        public string EvolutionId { get; }

        public EvolutionTrigger Trigger { get; }

        public string? EvolvedSpeciesId { get; }
    }
}
