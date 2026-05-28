using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Evolution.Events;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Evolution.Reactors
{

    /// <summary>
    /// Watches <see cref="LevelUpEvent"/> and fires any of the actor's registered
    /// <see cref="EvolutionTrigger.LevelUp"/> evolutions whose <see cref="EvolutionDefinition.RequiredLevel"/>
    /// is at or below the new level. Multi-level jumps fire every evolution that lands inside
    /// the level range (e.g. level 5 → 9 with evolutions at 6 and 8 both fire).
    ///
    /// Order: evolutions fire in dictionary order then by id — deterministic and stable. The
    /// game's reactors are responsible for any "only one evolution per level-up" gating.
    /// </summary>
    public sealed class LevelUpEvolutionReactor : IDomainEventReactor
    {
        public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
        {
            if (domainEvent is not LevelUpEvent levelUp)
            {
                return DomainResult.Success();
            }

            IReadOnlyCollection<string> evolutionIds = gameState.EvolutionState.GetEvolutions(levelUp.ActorId);
            if (evolutionIds.Count == 0)
            {
                return DomainResult.Success();
            }

            // Materialize ids in sorted order so firing is deterministic regardless of hash order.
            List<string> ordered = new(evolutionIds);
            ordered.Sort(System.StringComparer.Ordinal);

            foreach (string evolutionId in ordered)
            {
                if (!context.Definitions.TryGetEvolution(evolutionId, out EvolutionDefinition definition))
                {
                    continue;
                }

                if (definition.Trigger != EvolutionTrigger.LevelUp)
                {
                    continue;
                }

                if (definition.RequiredLevel < 1 || definition.RequiredLevel > levelUp.ToLevel)
                {
                    continue;
                }

                // The event already reflects a single-step level transition (FromLevel → ToLevel).
                // GrantExperienceCommand emits one event per level crossed, so a multi-level jump
                // produces a sequence of LevelUpEvents that this reactor handles individually.
                if (definition.RequiredLevel <= levelUp.FromLevel)
                {
                    // Already evolved on a prior level-up; don't re-fire.
                    continue;
                }

                context.EventSink.Publish(new EvolutionTriggeredEvent(
                    levelUp.ActorId,
                    definition.Id,
                    EvolutionTrigger.LevelUp,
                    definition.EvolvedSpeciesId));
            }

            return DomainResult.Success();
        }
    }
}
