using System;
using System.Collections.Generic;

namespace Moonforge.Core.World.Conditions
{

    public sealed class AllCondition : ICondition
    {
        public AllCondition(IReadOnlyList<ICondition> conditions)
        {
            Conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
        }

        public IReadOnlyList<ICondition> Conditions { get; }

        public bool Evaluate(GameState gameState)
        {
            foreach (ICondition condition in Conditions)
            {
                if (!condition.Evaluate(gameState))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
