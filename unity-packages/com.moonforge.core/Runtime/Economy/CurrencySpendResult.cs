namespace Moonforge.Core.Economy
{

    public sealed class CurrencySpendResult
    {
        public CurrencySpendResult(bool success, long previousBalance, long newBalance)
        {
            Success = success;
            PreviousBalance = previousBalance;
            NewBalance = newBalance;
        }

        public bool Success { get; }

        public long PreviousBalance { get; }

        public long NewBalance { get; }
    }
}
