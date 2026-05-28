using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Economy.Queries
{

    public sealed class GetCurrencyBalanceQuery : IQuery<long>
    {
        public GetCurrencyBalanceQuery(string currencyId)
        {
            CurrencyId = currencyId;
        }

        public string CurrencyId { get; }
    }
}
