using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events
{

    /// <summary>
    /// Generic signal emitted by the <c>EmitInteractionSignal</c> effect. Quest reactors and host
    /// scripts can listen for specific signal keys to drive game logic without the engine having
    /// to know about every interaction-specific behaviour.
    /// </summary>
    public sealed class InteractionSignalEvent : DomainEvent
    {
        public InteractionSignalEvent(string signalKey, string sourceInstanceId, string actorId)
            : base(nameof(InteractionSignalEvent))
        {
            SignalKey = signalKey;
            SourceInstanceId = sourceInstanceId;
            ActorId = actorId;
        }

        public string SignalKey { get; }

        public string SourceInstanceId { get; }

        public string ActorId { get; }
    }
}
