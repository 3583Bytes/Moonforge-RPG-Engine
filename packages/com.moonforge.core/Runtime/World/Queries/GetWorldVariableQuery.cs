using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.World.Queries
{

    public sealed class GetWorldVariableQuery : IQuery<WorldVariableValue?>
    {
        public GetWorldVariableQuery(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }
}
