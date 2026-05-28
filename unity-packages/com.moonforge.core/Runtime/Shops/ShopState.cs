using System;
using System.Collections.Generic;

namespace Moonforge.Core.Shops
{

    public sealed class ShopState
    {
        private readonly Dictionary<string, int> _entryStock = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _lastRestockMinute = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> EntryStock => _entryStock;

        public IReadOnlyDictionary<string, long> LastRestockMinutes => _lastRestockMinute;

        public int? TryGetStock(string shopId, string itemId)
        {
            if (_entryStock.TryGetValue(ToKey(shopId, itemId), out int value))
            {
                return value;
            }

            return null;
        }

        public int GetOrInitializeStock(string shopId, string itemId, int defaultValue)
        {
            string key = ToKey(shopId, itemId);
            if (_entryStock.TryGetValue(key, out int current))
            {
                return current;
            }

            _entryStock[key] = defaultValue;
            return defaultValue;
        }

        public void SetStock(string shopId, string itemId, int stock)
        {
            _entryStock[ToKey(shopId, itemId)] = stock;
        }

        public long? GetLastRestockMinute(string shopId)
        {
            if (_lastRestockMinute.TryGetValue(shopId, out long minute))
            {
                return minute;
            }

            return null;
        }

        public void SetLastRestockMinute(string shopId, long minute)
        {
            _lastRestockMinute[shopId] = minute;
        }

        public void CopyFrom(ShopState source)
        {
            _entryStock.Clear();
            foreach ((string key, int value) in source._entryStock)
            {
                _entryStock[key] = value;
            }

            _lastRestockMinute.Clear();
            foreach ((string key, long value) in source._lastRestockMinute)
            {
                _lastRestockMinute[key] = value;
            }
        }

        private static string ToKey(string shopId, string itemId)
        {
            return $"{shopId}|{itemId}";
        }
    }
}
