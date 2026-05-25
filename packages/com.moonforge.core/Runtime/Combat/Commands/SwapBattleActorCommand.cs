using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands;

/// <summary>
/// Swaps the current-turn party actor out of the active battle and brings in a fresh actor
/// to take their place. The swap counts as the current actor's action — the turn advances
/// after the swap resolves, exactly like <see cref="UseBattleSkillCommand"/>.
/// </summary>
/// <remarks>
/// The incoming actor is described by a fresh <see cref="BattleActorDefinition"/>; the caller
/// is responsible for constructing it with whatever HP, status, and skills the bench actor
/// should arrive with. The engine does not preserve per-battle state for swapped-out actors —
/// a subsequent swap-in must be supplied with the desired starting state.
/// </remarks>
public sealed class SwapBattleActorCommand : ICommand
{
    public SwapBattleActorCommand(string outActorId, BattleActorDefinition inActor)
    {
        OutActorId = outActorId;
        InActor = inActor;
    }

    public string OutActorId { get; }

    public BattleActorDefinition InActor { get; }
}
