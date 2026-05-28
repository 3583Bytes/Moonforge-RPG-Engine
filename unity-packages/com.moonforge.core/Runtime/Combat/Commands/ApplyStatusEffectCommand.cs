using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands
{

    public sealed class ApplyStatusEffectCommand : ICommand
    {
        public ApplyStatusEffectCommand(string actorId, string statusId, string? sourceActorId = null, int? durationOverride = null)
        {
            ActorId = actorId;
            StatusId = statusId;
            SourceActorId = sourceActorId;
            DurationOverride = durationOverride;
        }

        public string ActorId { get; }

        public string StatusId { get; }

        public string? SourceActorId { get; }

        public int? DurationOverride { get; }
    }
}
