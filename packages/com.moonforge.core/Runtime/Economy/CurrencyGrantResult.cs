namespace Moonforge.Core.Economy;

public sealed class CurrencyGrantResult
{
    public CurrencyGrantResult(long previousBalance, long newBalance, bool clamped)
    {
        PreviousBalance = previousBalance;
        NewBalance = newBalance;
        Clamped = clamped;
    }

    public long PreviousBalance { get; }

    public long NewBalance { get; }

    public bool Clamped { get; }
}
