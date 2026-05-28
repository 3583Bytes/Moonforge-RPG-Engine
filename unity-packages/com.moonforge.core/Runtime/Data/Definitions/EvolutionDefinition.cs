using System;
using Moonforge.Core.Evolution;

namespace Moonforge.Core.Data.Definitions
{

    /// <summary>
    /// Declarative trigger for one evolution. The engine handles detecting when conditions are
    /// met and emitting <see cref="Evolution.Events.EvolutionTriggeredEvent"/>; the game owns
    /// the outcome (stat/type/skill/display-name changes) by listening to that event and applying
    /// changes through the usual command primitives.
    ///
    /// Per-actor eligibility is registered with <see cref="Evolution.Commands.ConfigureActorEvolutionsCommand"/>
    /// — an actor only auto-evolves through definitions the game has assigned to it.
    /// </summary>
    public sealed class EvolutionDefinition
    {
        public EvolutionDefinition(
            string id,
            EvolutionTrigger trigger,
            int requiredLevel = 0,
            string? displayName = null,
            string? evolvedSpeciesId = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Evolution id is required.", nameof(id));
            }

            if (trigger == EvolutionTrigger.LevelUp && requiredLevel < 2)
            {
                throw new ArgumentException("LevelUp evolutions require a target level of 2 or higher.", nameof(requiredLevel));
            }

            Id = id;
            Trigger = trigger;
            RequiredLevel = requiredLevel < 0 ? 0 : requiredLevel;
            DisplayName = displayName;
            EvolvedSpeciesId = string.IsNullOrWhiteSpace(evolvedSpeciesId) ? null : evolvedSpeciesId;
        }

        public string Id { get; }

        public EvolutionTrigger Trigger { get; }

        /// <summary>Required actor level for <see cref="EvolutionTrigger.LevelUp"/>. Ignored for Manual.</summary>
        public int RequiredLevel { get; }

        public string? DisplayName { get; }

        /// <summary>
        /// Optional game-meaningful tag describing what the actor becomes. The engine doesn't
        /// interpret it — it's carried through to <see cref="Evolution.Events.EvolutionTriggeredEvent"/>
        /// so the game's reactor can route the right content change (e.g. swap species template).
        /// </summary>
        public string? EvolvedSpeciesId { get; }
    }
}
