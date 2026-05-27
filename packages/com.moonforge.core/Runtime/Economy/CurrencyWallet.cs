using System;
using System.Collections.Generic;

namespace Moonforge.Core.Economy
{

    public sealed class CurrencyWallet
    {
        private readonly Dictionary<string, long> _balances = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _maxBalances = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, long> Balances => _balances;

        public void ConfigureMax(string currencyId, long maxValue)
        {
            if (string.IsNullOrWhiteSpace(currencyId))
            {
                throw new ArgumentException("Currency ID is required.", nameof(currencyId));
            }

            if (maxValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            }

            _maxBalances[currencyId] = maxValue;
        }

        public long GetMax(string currencyId)
        {
            return _maxBalances.TryGetValue(currencyId, out long max) ? max : long.MaxValue;
        }

        public long GetBalance(string currencyId)
        {
            return _balances.TryGetValue(currencyId, out long balance) ? balance : 0;
        }

        public CurrencyGrantResult Grant(string currencyId, long amount)
        {
            if (string.IsNullOrWhiteSpace(currencyId))
            {
                throw new ArgumentException("Currency ID is required.", nameof(currencyId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            long previous = GetBalance(currencyId);
            long raw;
            try
            {
                raw = checked(previous + amount);
            }
            catch (OverflowException)
            {
                raw = long.MaxValue;
            }

            long max = GetMax(currencyId);
            bool clamped = raw > max;
            long next = clamped ? max : raw;
            _balances[currencyId] = next;
            return new CurrencyGrantResult(previous, next, clamped);
        }

        public CurrencySpendResult Spend(string currencyId, long amount)
        {
            if (string.IsNullOrWhiteSpace(currencyId))
            {
                throw new ArgumentException("Currency ID is required.", nameof(currencyId));
            }

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            long previous = GetBalance(currencyId);
            if (previous < amount)
            {
                return new CurrencySpendResult(false, previous, previous);
            }

            long next = previous - amount;
            _balances[currencyId] = next;
            return new CurrencySpendResult(true, previous, next);
        }

        public void CopyFrom(CurrencyWallet source)
        {
            _balances.Clear();
            foreach ((string key, long value) in source._balances)
            {
                _balances[key] = value;
            }

            _maxBalances.Clear();
            foreach ((string key, long value) in source._maxBalances)
            {
                _maxBalances[key] = value;
            }
        }
    }
}
