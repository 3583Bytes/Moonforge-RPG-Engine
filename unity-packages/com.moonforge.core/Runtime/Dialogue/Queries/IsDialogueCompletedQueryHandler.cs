using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Dialogue.Queries
{

    public sealed class IsDialogueCompletedQueryHandler : IQueryHandler<IsDialogueCompletedQuery, bool>
    {
        public bool Query(GameState gameState, IsDialogueCompletedQuery query)
        {
            if (!gameState.DialogueState.TryGet(query.DialogueId, out DialogueInstanceState instance))
            {
                return false;
            }

            return instance.Completed;
        }
    }
}
