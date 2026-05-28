using System;
using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class EquipmentDefinition
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyBonuses =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly IReadOnlyList<string> EmptyGrantedSkills = Array.Empty<string>();

        public EquipmentDefinition(
            string itemId,
            string slotId,
            IReadOnlyDictionary<string, int>? statBonuses = null,
            string? displayName = null,
            string? description = null,
            IReadOnlyList<string>? grantedSkillIds = null)
        {
            ItemId = itemId;
            SlotId = slotId;
            StatBonuses = statBonuses ?? EmptyBonuses;
            DisplayName = displayName;
            Description = description;
            GrantedSkillIds = grantedSkillIds ?? EmptyGrantedSkills;
        }

        public string ItemId { get; }

        public string SlotId { get; }

        public IReadOnlyDictionary<string, int> StatBonuses { get; }

        public string? DisplayName { get; }

        public string? Description { get; }

        /// <summary>
        /// Skill ids granted to the wearer while this item is equipped. The engine doesn't
        /// auto-merge these into <see cref="Moonforge.Core.Combat.BattleActorDefinition.SkillIds"/>;
        /// query <c>GetEquipmentGrantedSkillsQuery</c> when assembling a battle actor and
        /// merge with the actor's base learned skills there.
        /// </summary>
        public IReadOnlyList<string> GrantedSkillIds { get; }
    }
}
