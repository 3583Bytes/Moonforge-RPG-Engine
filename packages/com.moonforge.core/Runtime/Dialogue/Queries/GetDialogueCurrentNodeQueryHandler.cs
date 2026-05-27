using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Dialogue.Queries
{

    public sealed class GetDialogueCurrentNodeQueryHandler : IQueryHandler<GetDialogueCurrentNodeQuery, string?>
    {
        public string? Query(GameState gameState, GetDialogueCurrentNodeQuery query)
        {
            if (!gameState.DialogueState.TryGet(query.DialogueId, out DialogueInstanceState instance))
            {
                return null;
            }

            return instance.CurrentNodeId;
        }
    }
}
