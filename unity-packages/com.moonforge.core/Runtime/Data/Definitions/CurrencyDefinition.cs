namespace Moonforge.Core.Data.Definitions
{

    public sealed class CurrencyDefinition
    {
        public CurrencyDefinition(string id, long maxBalance, string? displayName = null)
        {
            Id = id;
            MaxBalance = maxBalance;
            DisplayName = displayName;
        }

        public string Id { get; }

        public long MaxBalance { get; }

        public string? DisplayName { get; }
    }
}
