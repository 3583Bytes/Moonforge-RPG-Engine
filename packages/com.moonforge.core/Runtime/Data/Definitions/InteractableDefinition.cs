using System;
using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class InteractableDefinition
{
    private static readonly IReadOnlyList<InteractableEffectDefinition> EmptyEffects =
        System.Array.Empty<InteractableEffectDefinition>();

    public InteractableDefinition(
        string id,
        IReadOnlyList<InteractableEffectDefinition>? effects = null,
        bool blocksMovement = false,
        bool blocksLineOfSight = false,
        int maxUses = 1,
        bool startsLocked = false,
        string? requiredKeyItemId = null,
        bool consumeKeyOnUnlock = true,
        string? displayName = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Interactable ID is required.", nameof(id));
        }

        if (maxUses == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUses), "MaxUses must be positive or -1 (unlimited).");
        }

        Id = id;
        Effects = effects ?? EmptyEffects;
        BlocksMovement = blocksMovement;
        BlocksLineOfSight = blocksLineOfSight;
        MaxUses = maxUses;
        StartsLocked = startsLocked;
        RequiredKeyItemId = string.IsNullOrWhiteSpace(requiredKeyItemId) ? null : requiredKeyItemId;
        ConsumeKeyOnUnlock = consumeKeyOnUnlock;
        DisplayName = displayName;
        Description = description;
    }

    public string Id { get; }

    public IReadOnlyList<InteractableEffectDefinition> Effects { get; }

    public bool BlocksMovement { get; }

    public bool BlocksLineOfSight { get; }

    /// <summary><c>-1</c> for unlimited; otherwise positive.</summary>
    public int MaxUses { get; }

    public bool StartsLocked { get; }

    public string? RequiredKeyItemId { get; }

    public bool ConsumeKeyOnUnlock { get; }

    public string? DisplayName { get; }

    public string? Description { get; }
}
