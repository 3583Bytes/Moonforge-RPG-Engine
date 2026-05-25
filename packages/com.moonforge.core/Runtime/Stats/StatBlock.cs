using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Formulas;

namespace Moonforge.Core.Stats;

/// <summary>
/// Mutable per-actor stat state. Stores base values for primary (stored) stats and a list of
/// active <see cref="StatModifier"/> contributions. The pipeline (Flat → AddPercent →
/// MultPercent → Override) is applied each time <see cref="Get"/> is called.
/// </summary>
public sealed class StatBlock
{
    private readonly Dictionary<string, int> _base = new(StringComparer.Ordinal);
    private readonly List<StatModifier> _modifiers = new();

    public IReadOnlyDictionary<string, int> Base => _base;

    public IReadOnlyList<StatModifier> Modifiers => _modifiers;

    public bool TryGetBase(string statId, out int value)
    {
        return _base.TryGetValue(statId, out value);
    }

    public void SetBase(string statId, int value)
    {
        if (string.IsNullOrWhiteSpace(statId))
        {
            throw new ArgumentException("Stat ID is required.", nameof(statId));
        }

        _base[statId] = value;
    }

    public bool RemoveBase(string statId)
    {
        return _base.Remove(statId);
    }

    public void AddModifier(StatModifier modifier)
    {
        if (modifier is null)
        {
            throw new ArgumentNullException(nameof(modifier));
        }

        _modifiers.Add(modifier);
    }

    /// <summary>
    /// Removes every modifier whose <see cref="StatModifier.SourceKind"/> and
    /// <see cref="StatModifier.SourceId"/> match. Returns the number of modifiers removed.
    /// </summary>
    public int RemoveModifiersBySource(string sourceKind, string sourceId)
    {
        int removed = 0;
        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            StatModifier mod = _modifiers[i];
            if (string.Equals(mod.SourceKind, sourceKind, StringComparison.Ordinal)
                && string.Equals(mod.SourceId, sourceId, StringComparison.Ordinal))
            {
                _modifiers.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Computes the effective value of <paramref name="statId"/> applying the modifier pipeline.
    /// Derived stats are evaluated through <paramref name="formulas"/>. The optional
    /// <paramref name="extraVars"/> are merged with the actor's base stats and made available
    /// to the formula evaluator (e.g. <c>level</c>).
    /// </summary>
    public int Get(
        string statId,
        IGameDefinitionCatalog? definitions,
        IFormulaEvaluator formulas,
        IReadOnlyDictionary<string, double>? extraVars = null,
        int fallbackBase = 0)
    {
        StatDefinition? def = null;
        if (definitions is not null && definitions.TryGetStat(statId, out StatDefinition resolved))
        {
            def = resolved;
        }

        double baseValue = ComputeBase(statId, def, definitions, formulas, extraVars, fallbackBase);
        double afterFlat = baseValue;
        double sumAddPercent = 0;
        List<double>? mults = null;
        StatModifier? bestOverride = null;

        // Sort by (Bucket, SourceKind, SourceId) for deterministic ordering before each pass.
        List<StatModifier> ordered = new(_modifiers);
        ordered.Sort(CompareForStat);

        foreach (StatModifier mod in ordered)
        {
            if (!string.Equals(mod.StatId, statId, StringComparison.Ordinal))
            {
                continue;
            }

            switch (mod.Bucket)
            {
                case StatModifierBucket.Flat:
                    afterFlat += mod.Value;
                    break;
                case StatModifierBucket.AddPercent:
                    sumAddPercent += mod.Value;
                    break;
                case StatModifierBucket.MultPercent:
                    mults ??= new List<double>();
                    mults.Add(mod.Value);
                    break;
                case StatModifierBucket.Override:
                    if (bestOverride is null
                        || mod.Priority > bestOverride.Priority
                        || (mod.Priority == bestOverride.Priority
                            && string.CompareOrdinal(mod.SourceId, bestOverride.SourceId) < 0))
                    {
                        bestOverride = mod;
                    }
                    break;
            }
        }

        double afterAdd = afterFlat * (1.0 + sumAddPercent);
        double afterMult = afterAdd;
        if (mults is not null)
        {
            for (int i = 0; i < mults.Count; i++)
            {
                afterMult *= (1.0 + mults[i]);
            }
        }

        double effective = bestOverride is not null ? bestOverride.Value : afterMult;
        int result = (int)Math.Round(effective, MidpointRounding.AwayFromZero);

        if (def is not null)
        {
            if (def.Min.HasValue && result < def.Min.Value) result = def.Min.Value;
            if (def.Max.HasValue && result > def.Max.Value) result = def.Max.Value;
        }

        return result;
    }

    public StatBlock Clone()
    {
        StatBlock clone = new();
        foreach (KeyValuePair<string, int> pair in _base)
        {
            clone._base[pair.Key] = pair.Value;
        }

        foreach (StatModifier mod in _modifiers)
        {
            clone._modifiers.Add(mod.Clone());
        }

        return clone;
    }

    private double ComputeBase(
        string statId,
        StatDefinition? def,
        IGameDefinitionCatalog? definitions,
        IFormulaEvaluator formulas,
        IReadOnlyDictionary<string, double>? extraVars,
        int fallbackBase)
    {
        if (def is not null && !string.IsNullOrWhiteSpace(def.DerivedFromFormula))
        {
            Dictionary<string, double> variables = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in _base)
            {
                variables[pair.Key] = pair.Value;
            }

            if (extraVars is not null)
            {
                foreach (KeyValuePair<string, double> pair in extraVars)
                {
                    variables[pair.Key] = pair.Value;
                }
            }

            return formulas.Evaluate(def.DerivedFromFormula!, variables);
        }

        if (_base.TryGetValue(statId, out int stored))
        {
            return stored;
        }

        // Registered stats use their declared default; unregistered stats inherit from the
        // caller-supplied fallback. This lets a block partially override scalar fields:
        // an actor can store only the stats it cares about while the rest fall through.
        return def is not null ? def.DefaultBase : fallbackBase;
    }

    private static int CompareForStat(StatModifier a, StatModifier b)
    {
        int byBucket = ((int)a.Bucket).CompareTo((int)b.Bucket);
        if (byBucket != 0) return byBucket;
        int byKind = string.CompareOrdinal(a.SourceKind, b.SourceKind);
        if (byKind != 0) return byKind;
        return string.CompareOrdinal(a.SourceId, b.SourceId);
    }
}
