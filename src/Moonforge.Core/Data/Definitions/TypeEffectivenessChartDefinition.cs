using System;
using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

/// <summary>
/// A 2-axis effectiveness table: rows are attack type ids, columns are defender type ids,
/// values are percent multipliers (100 = 1×, 200 = 2× super-effective, 50 = ½× resisted,
/// 0 = immune). Defenders may have multiple types — see <see cref="GetMultiplierPercent"/>
/// for how multi-type stacking works.
///
/// Charts are content, not state: register them with <see cref="IGameDefinitionCatalog"/>
/// and reference by id from <see cref="DamageTypeDefinition.EffectivenessChartId"/>.
/// </summary>
public sealed class TypeEffectivenessChartDefinition
{
    private readonly Dictionary<string, Dictionary<string, int>> _byAttack;

    public TypeEffectivenessChartDefinition(
        string id,
        IReadOnlyList<TypeEffectivenessEntry> entries,
        string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Chart id is required.", nameof(id));
        }

        Id = id;
        DisplayName = displayName;
        _byAttack = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        if (entries is null)
        {
            return;
        }

        foreach (TypeEffectivenessEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.AttackTypeId) || string.IsNullOrWhiteSpace(entry.DefenderTypeId))
            {
                continue;
            }

            if (!_byAttack.TryGetValue(entry.AttackTypeId, out Dictionary<string, int>? row))
            {
                row = new Dictionary<string, int>(StringComparer.Ordinal);
                _byAttack[entry.AttackTypeId] = row;
            }

            row[entry.DefenderTypeId] = entry.MultiplierPercent;
        }
    }

    public string Id { get; }

    public string? DisplayName { get; }

    /// <summary>
    /// Returns the multiplier (in whole percent) for an attack of <paramref name="attackTypeId"/>
    /// against a defender carrying <paramref name="defenderTypeIds"/>. Multiple defender types
    /// stack <b>multiplicatively</b>: e.g. a Grass/Steel defender hit by Fire (2× vs Grass,
    /// 2× vs Steel) takes 4×; a Water/Ground defender hit by Electric (2× vs Water, 0× vs
    /// Ground) takes 0.
    ///
    /// Missing entries are treated as 100 (1× neutral). An empty defender type list always
    /// returns 100.
    /// </summary>
    public int GetMultiplierPercent(string attackTypeId, IReadOnlyList<string>? defenderTypeIds)
    {
        if (defenderTypeIds is null || defenderTypeIds.Count == 0)
        {
            return 100;
        }

        if (!_byAttack.TryGetValue(attackTypeId, out Dictionary<string, int>? row))
        {
            return 100;
        }

        long product = 100;
        for (int i = 0; i < defenderTypeIds.Count; i++)
        {
            string defenderType = defenderTypeIds[i];
            if (string.IsNullOrWhiteSpace(defenderType))
            {
                continue;
            }

            int multiplier = row.TryGetValue(defenderType, out int entry) ? entry : 100;
            if (multiplier == 0)
            {
                return 0;
            }

            product = product * multiplier / 100;
        }

        if (product > int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)product;
    }
}

public readonly struct TypeEffectivenessEntry
{
    public TypeEffectivenessEntry(string attackTypeId, string defenderTypeId, int multiplierPercent)
    {
        AttackTypeId = attackTypeId;
        DefenderTypeId = defenderTypeId;
        MultiplierPercent = multiplierPercent < 0 ? 0 : multiplierPercent;
    }

    public string AttackTypeId { get; }

    public string DefenderTypeId { get; }

    public int MultiplierPercent { get; }
}
