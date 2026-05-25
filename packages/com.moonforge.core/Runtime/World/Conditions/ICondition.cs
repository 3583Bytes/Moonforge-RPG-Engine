using Moonforge.Core;

namespace Moonforge.Core.World.Conditions;

public interface ICondition
{
    bool Evaluate(GameState gameState);
}
