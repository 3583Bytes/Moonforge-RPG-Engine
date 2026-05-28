using System.Collections.Generic;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Stats.Queries
{

    public sealed class GetStatQuery : IQuery<int>
    {
        public GetStatQuery(string actorId, string statId, IReadOnlyDictionary<string, double>? extraVars = null)
        {
            ActorId = actorId;
            StatId = statId;
            ExtraVars = extraVars;
        }

        public string ActorId { get; }

        public string StatId { get; }

        public IReadOnlyDictionary<string, double>? ExtraVars { get; }
    }
}
