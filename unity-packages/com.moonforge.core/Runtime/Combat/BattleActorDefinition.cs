using System;
using System.Collections.Generic;

namespace Moonforge.Core.Combat
{

    public sealed class BattleActorDefinition
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyResourceMap =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly IReadOnlyList<string> EmptyTypeList = System.Array.Empty<string>();

        public BattleActorDefinition(
            string actorId,
            string displayName,
            CombatFaction faction,
            int maxHp,
            int atk,
            int def,
            int matk,
            int mdef,
            int initiative,
            IReadOnlyList<string> skillIds,
            bool playerControlled = false,
            BattleAiPolicyDefinition? aiPolicy = null,
            IReadOnlyDictionary<string, int>? resourceMaxes = null,
            IReadOnlyDictionary<string, int>? startingResources = null,
            IReadOnlyDictionary<string, int>? resourceRefreshPerTurn = null,
            long xpReward = 0,
            IReadOnlyList<string>? defenderTypeIds = null,
            int captureBaseRate = 0,
            string? speciesId = null)
        {
            ActorId = actorId;
            DisplayName = displayName;
            Faction = faction;
            MaxHp = maxHp;
            Atk = atk;
            Def = def;
            Matk = matk;
            Mdef = mdef;
            Initiative = initiative;
            SkillIds = skillIds ?? System.Array.Empty<string>();
            PlayerControlled = playerControlled;
            AiPolicy = aiPolicy;
            ResourceMaxes = resourceMaxes ?? EmptyResourceMap;
            StartingResources = startingResources ?? EmptyResourceMap;
            ResourceRefreshPerTurn = resourceRefreshPerTurn ?? EmptyResourceMap;
            XpReward = xpReward < 0 ? 0 : xpReward;
            DefenderTypeIds = defenderTypeIds ?? EmptyTypeList;
            CaptureBaseRate = captureBaseRate < 0 ? 0 : (captureBaseRate > 100 ? 100 : captureBaseRate);
            SpeciesId = string.IsNullOrWhiteSpace(speciesId) ? null : speciesId;
        }

        public string ActorId { get; }

        public string DisplayName { get; }

        public CombatFaction Faction { get; }

        public int MaxHp { get; }

        public int Atk { get; }

        public int Def { get; }

        public int Matk { get; }

        public int Mdef { get; }

        public int Initiative { get; }

        public IReadOnlyList<string> SkillIds { get; }

        public bool PlayerControlled { get; }

        public BattleAiPolicyDefinition? AiPolicy { get; }

        public IReadOnlyDictionary<string, int> ResourceMaxes { get; }

        public IReadOnlyDictionary<string, int> StartingResources { get; }

        public IReadOnlyDictionary<string, int> ResourceRefreshPerTurn { get; }

        public long XpReward { get; }

        /// <summary>
        /// The actor's "types" for type-chart effectiveness lookups (e.g. Pokemon's Grass/Poison
        /// dual typing). Empty list means the actor is untyped — chart lookups will always return
        /// 1× neutral against them.
        /// </summary>
        public IReadOnlyList<string> DefenderTypeIds { get; }

        /// <summary>
        /// Base capture rate in whole percent (0-100). 0 means "cannot be captured" (default,
        /// covers bosses and story enemies). Higher values are easier to catch. The actual
        /// capture chance also factors in the target's HP percentage and any per-attempt bonus
        /// (e.g. ball quality), so the effective chance is typically much lower than this base
        /// — see <see cref="Commands.AttemptCaptureCommand"/>.
        /// </summary>
        public int CaptureBaseRate { get; }

        /// <summary>
        /// Optional "species" tag — a shared identifier across actor instances that belong to
        /// the same monster type (e.g. all "pidgey.001", "pidgey.002" actors might share
        /// <c>"species.pidgey"</c>). Drives bestiary tracking; ignored when null.
        /// </summary>
        public string? SpeciesId { get; }
    }
}
