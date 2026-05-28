using Moonforge.Core.Bestiary.Events;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Bestiary.Reactors
{

    /// <summary>
    /// Auto-fills the bestiary. On <see cref="BattleStartedEvent"/> every enemy actor with a
    /// <see cref="BattleActorDefinition.SpeciesId"/> is marked encountered; on
    /// <see cref="BattleActorCapturedEvent"/> the captured species is marked captured (and
    /// encountered too, if not already). Untagged actors are ignored, so games that don't care
    /// about the bestiary simply leave <c>SpeciesId</c> null and pay no cost.
    /// </summary>
    public sealed class BestiaryAutoTrackReactor : IDomainEventReactor
    {
        public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
        {
            switch (domainEvent)
            {
                case BattleStartedEvent:
                    return HandleBattleStarted(gameState, context);

                case BattleActorCapturedEvent captured:
                    return HandleCapture(gameState, context, captured);
            }

            return DomainResult.Success();
        }

        private static DomainResult HandleBattleStarted(GameState gameState, CommandContext context)
        {
            BattleState? battle = gameState.ActiveBattle;
            if (battle is null)
            {
                return DomainResult.Success();
            }

            long minutes = context.Clock.CurrentSimulationMinutes;
            foreach (BattleActorState actor in battle.Actors.Values)
            {
                if (actor.Faction != CombatFaction.Enemy || string.IsNullOrWhiteSpace(actor.SpeciesId))
                {
                    continue;
                }

                bool first = gameState.BestiaryState.RecordEncounter(actor.SpeciesId!, minutes);
                if (first)
                {
                    context.EventSink.Publish(new SpeciesFirstEncounteredEvent(actor.SpeciesId!, minutes));
                }
            }

            return DomainResult.Success();
        }

        private static DomainResult HandleCapture(GameState gameState, CommandContext context, BattleActorCapturedEvent captured)
        {
            if (string.IsNullOrWhiteSpace(captured.CapturedSpeciesId))
            {
                return DomainResult.Success();
            }

            long minutes = context.Clock.CurrentSimulationMinutes;
            // Capture implies encounter; record both so a sneak-captured species (no prior battle)
            // still shows in the bestiary's encountered tab.
            bool firstEncounter = gameState.BestiaryState.RecordEncounter(captured.CapturedSpeciesId!, minutes);
            if (firstEncounter)
            {
                context.EventSink.Publish(new SpeciesFirstEncounteredEvent(captured.CapturedSpeciesId!, minutes));
            }

            bool firstCapture = gameState.BestiaryState.RecordCapture(captured.CapturedSpeciesId!, minutes);
            if (firstCapture)
            {
                context.EventSink.Publish(new SpeciesFirstCapturedEvent(captured.CapturedSpeciesId!, minutes));
            }

            return DomainResult.Success();
        }
    }
}
