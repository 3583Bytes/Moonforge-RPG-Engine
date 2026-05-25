using System.Collections.Generic;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Equipment.Queries;

/// <summary>
/// Returns the deduplicated union of every <see cref="Moonforge.Core.Data.Definitions.EquipmentDefinition.GrantedSkillIds"/>
/// across currently equipped items. Iteration order follows
/// <see cref="EquipmentState.EquippedItems"/> for determinism; within an item the original
/// order of <c>GrantedSkillIds</c> is preserved.
/// </summary>
public sealed class GetEquipmentGrantedSkillsQuery : IQuery<IReadOnlyList<string>>
{
}
