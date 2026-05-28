using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Progression.Commands
{

    public sealed class ConfigureActorProgressionCommand : ICommand
    {
        public ConfigureActorProgressionCommand(string actorId, string curveId, int level = 1, long xp = 0)
        {
            ActorId = actorId;
            CurveId = curveId;
            Level = level;
            Xp = xp;
        }

        public string ActorId { get; }

        public string CurveId { get; }

        public int Level { get; }

        public long Xp { get; }
    }
}
