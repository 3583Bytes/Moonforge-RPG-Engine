using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Equipment.Queries
{

    public sealed class GetEquipmentBonusesQueryHandler : IQueryHandler<GetEquipmentBonusesQuery, IReadOnlyDictionary<string, int>>
    {
        private readonly IGameDefinitionCatalog _definitions;

        public GetEquipmentBonusesQueryHandler(IGameDefinitionCatalog definitions)
        {
            _definitions = definitions;
        }

        public IReadOnlyDictionary<string, int> Query(GameState gameState, GetEquipmentBonusesQuery query)
        {
            Dictionary<string, int> totals = new(StringComparer.Ordinal);

            // Sort by slot so results are independent of dictionary insertion order.
            List<KeyValuePair<string, string>> equipped = new(gameState.EquipmentState.EquippedItems);
            equipped.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));

            foreach (KeyValuePair<string, string> pair in equipped)
            {
                if (!_definitions.TryGetEquipment(pair.Value, out EquipmentDefinition equipmentDefinition))
                {
                    continue;
                }

                foreach (KeyValuePair<string, int> bonus in equipmentDefinition.StatBonuses)
                {
                    if (totals.TryGetValue(bonus.Key, out int existing))
                    {
                        totals[bonus.Key] = existing + bonus.Value;
                    }
                    else
                    {
                        totals[bonus.Key] = bonus.Value;
                    }
                }
            }

            return totals;
        }
    }
}
