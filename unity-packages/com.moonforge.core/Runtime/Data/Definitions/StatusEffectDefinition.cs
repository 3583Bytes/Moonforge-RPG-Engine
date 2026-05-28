using System;
using System.Collections.Generic;
using Moonforge.Core.Combat;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class StatusEffectDefinition
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyMods =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public StatusEffectDefinition(
            string id,
            int durationTurns,
            int tickHpDelta = 0,
            IReadOnlyDictionary<string, int>? statModifiers = null,
            bool preventsAction = false,
            StatusStackPolicy stackPolicy = StatusStackPolicy.RefreshDuration,
            string? displayName = null,
            string? description = null)
        {
            Id = id;
            DurationTurns = durationTurns < 0 ? 0 : durationTurns;
            TickHpDelta = tickHpDelta;
            StatModifiers = statModifiers ?? EmptyMods;
            PreventsAction = preventsAction;
            StackPolicy = stackPolicy;
            DisplayName = displayName;
            Description = description;
        }

        public string Id { get; }

        public int DurationTurns { get; }

        public int TickHpDelta { get; }

        public IReadOnlyDictionary<string, int> StatModifiers { get; }

        public bool PreventsAction { get; }

        public StatusStackPolicy StackPolicy { get; }

        public string? DisplayName { get; }

        public string? Description { get; }
    }
}
