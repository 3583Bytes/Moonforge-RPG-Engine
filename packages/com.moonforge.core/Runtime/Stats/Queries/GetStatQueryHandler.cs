using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Progression;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Stats.Queries;

public sealed class GetStatQueryHandler : IQueryHandler<GetStatQuery, int>
{
    private readonly IGameDefinitionCatalog _definitions;
    private readonly IFormulaEvaluator _formulas;

    public GetStatQueryHandler(IGameDefinitionCatalog definitions, IFormulaEvaluator formulas)
    {
        _definitions = definitions;
        _formulas = formulas;
    }

    public int Query(GameState gameState, GetStatQuery query)
    {
        if (!gameState.ActorStatsState.TryGet(query.ActorId, out StatBlock block))
        {
            return 0;
        }

        Dictionary<string, double>? merged = null;
        if (gameState.ProgressionState.TryGet(query.ActorId, out ActorProgression progression))
        {
            merged = new Dictionary<string, double>(StringComparer.Ordinal) { ["level"] = progression.Level };
        }

        if (query.ExtraVars is not null)
        {
            merged ??= new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, double> pair in query.ExtraVars)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return block.Get(query.StatId, _definitions, _formulas, merged);
    }
}
