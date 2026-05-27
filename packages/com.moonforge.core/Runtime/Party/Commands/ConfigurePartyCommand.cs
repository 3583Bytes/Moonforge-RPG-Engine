using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Party.Commands
{

    public sealed class ConfigurePartyCommand : ICommand
    {
        public ConfigurePartyCommand(int maxActive, int maxRoster)
        {
            MaxActive = maxActive;
            MaxRoster = maxRoster;
        }

        public int MaxActive { get; }

        public int MaxRoster { get; }
    }
}
