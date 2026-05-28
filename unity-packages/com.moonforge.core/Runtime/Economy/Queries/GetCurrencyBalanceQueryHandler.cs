using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Economy.Queries
{

    public sealed class GetCurrencyBalanceQueryHandler : IQueryHandler<GetCurrencyBalanceQuery, long>
    {
        public long Query(GameState gameState, GetCurrencyBalanceQuery query)
        {
            return gameState.CurrencyWallet.GetBalance(query.CurrencyId);
        }
    }
}
