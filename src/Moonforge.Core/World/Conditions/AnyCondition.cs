using System;
using System.Collections.Generic;

namespace Moonforge.Core.World.Conditions;

public sealed class AnyCondition : ICondition
{
    public AnyCondition(IReadOnlyList<ICondition> conditions)
    {
        Conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
    }

    public IReadOnlyList<ICondition> Conditions { get; }

    public bool Evaluate(GameState gameState)
    {
        foreach (ICondition condition in Conditions)
        {
            if (condition.Evaluate(gameState))
            {
                return true;
            }
        }

        return false;
    }
}
