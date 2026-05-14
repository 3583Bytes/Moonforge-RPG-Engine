using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Stats.Commands;

public sealed class ApplyStatModifierCommand : ICommand
{
    public ApplyStatModifierCommand(string actorId, StatModifier modifier)
    {
        ActorId = actorId;
        Modifier = modifier;
    }

    public string ActorId { get; }

    public StatModifier Modifier { get; }
}
