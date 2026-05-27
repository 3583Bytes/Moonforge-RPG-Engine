using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.World.Commands
{

    public sealed class SetWorldVariableCommand : ICommand
    {
        public SetWorldVariableCommand(string key, WorldVariableValue value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        public WorldVariableValue Value { get; }
    }
}
