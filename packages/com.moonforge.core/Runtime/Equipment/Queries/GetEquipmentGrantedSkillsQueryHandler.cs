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

            foreach (KeyValuePair<string, string> pair in gameState.EquipmentState.EquippedItems)
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
