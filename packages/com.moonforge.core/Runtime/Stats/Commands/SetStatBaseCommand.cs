using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Stats.Commands
{

    public sealed class SetStatBaseCommand : ICommand
    {
        public SetStatBaseCommand(string actorId, string statId, int value)
        {
            ActorId = actorId;
            StatId = statId;
            Value = value;
        }

        public string ActorId { get; }

        public string StatId { get; }

        public int Value { get; }
    }
}
