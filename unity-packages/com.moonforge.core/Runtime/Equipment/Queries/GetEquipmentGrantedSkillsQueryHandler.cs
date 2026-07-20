using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Equipment.Queries
{

    public sealed class GetEquipmentGrantedSkillsQueryHandler : IQueryHandler<GetEquipmentGrantedSkillsQuery, IReadOnlyList<string>>
    {
        private readonly IGameDefinitionCatalog _definitions;

        public GetEquipmentGrantedSkillsQueryHandler(IGameDefinitionCatalog definitions)
        {
            _definitions = definitions;
        }

        public IReadOnlyList<string> Query(GameState gameState, GetEquipmentGrantedSkillsQuery query)
        {
            List<string> ordered = new();
            HashSet<string> seen = new(StringComparer.Ordinal);

            // Sort by slot so the returned skill order is independent of dictionary
            // insertion order (it feeds battle skill lists, which must be deterministic).
            List<KeyValuePair<string, string>> equipped = new(gameState.EquipmentState.EquippedItems);
            equipped.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));

            foreach (KeyValuePair<string, string> pair in equipped)
            {
                if (!_definitions.TryGetEquipment(pair.Value, out EquipmentDefinition equipmentDefinition))
                {
                    continue;
                }

                for (int i = 0; i < equipmentDefinition.GrantedSkillIds.Count; i++)
                {
                    string skillId = equipmentDefinition.GrantedSkillIds[i];
                    if (string.IsNullOrWhiteSpace(skillId))
                    {
                        continue;
                    }

                    if (seen.Add(skillId))
                    {
                        ordered.Add(skillId);
                    }
                }
            }

            return ordered;
        }
    }
}
