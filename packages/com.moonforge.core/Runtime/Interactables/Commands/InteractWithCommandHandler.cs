using System;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Exploration;
using Moonforge.Core.Interactables.Events;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;

namespace Moonforge.Core.Interactables.Commands;

public sealed class InteractWithCommandHandler : ICommandHandler<InteractWithCommand>
{
    private readonly RollAndGrantLootCommandHandler _lootHandler = new();
    private readonly SetWorldVariableCommandHandler _worldHandler = new();

    public DomainResult Handle(GameState gameState, InteractWithCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
        }

        if (!gameState.InteractablesState.TryGet(command.InstanceId, out InteractableInstance instance))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"No interactable instance '{command.InstanceId}'."));
        }

        if (!context.Definitions.TryGetInteractable(instance.DefinitionId, out InteractableDefinition definition))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Unknown interactable definition '{instance.DefinitionId}'."));
        }

        if (instance.Status == InteractableStatus.Consumed || instance.Status == InteractableStatus.Broken)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.Conflict,
                $"Interactable '{command.InstanceId}' is no longer interactive."));
        }

        if (!gameState.ExplorationState.TryGetActor(command.ActorId, out ExplorationActorState actor))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Actor '{command.ActorId}' is not on the exploration map."));
        }

        if (!WithinInteractionRange(actor, instance.Position))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                $"Actor '{command.ActorId}' is not adjacent to '{command.InstanceId}'."));
        }

        if (instance.Locked)
        {
            (DomainResult unlockResult, bool unlocked) = TryUnlock(gameState, instance, definition, command, context);
            if (!unlockResult.IsSuccess)
            {
                return unlockResult;
            }

            if (!unlocked)
            {
                // Lock prevented the action; InteractableLockedEvent already published.
                // Nothing else to do — no effects, no use decrement, no status change.
                return DomainResult.Success();
            }
        }

        foreach (InteractableEffectDefinition effect in definition.Effects)
        {
            DomainResult effectResult = ApplyEffect(gameState, instance, effect, command, context);
            if (!effectResult.IsSuccess)
            {
                return effectResult;
            }
        }

        if (instance.UsesRemaining > 0)
        {
            instance.UsesRemaining--;
        }

        InteractableStatus previousStatus = instance.Status;
        if (instance.UsesRemaining == 0)
        {
            instance.Status = InteractableStatus.Consumed;
        }
        else if (previousStatus == InteractableStatus.Default)
        {
            instance.Status = InteractableStatus.Opened;
        }

        context.EventSink.Publish(new InteractableInteractedEvent(instance.InstanceId, instance.DefinitionId, command.ActorId));
        if (instance.Status != previousStatus)
        {
            context.EventSink.Publish(new InteractableStatusChangedEvent(instance.InstanceId, previousStatus, instance.Status));
        }

        if (instance.Status == InteractableStatus.Consumed)
        {
            context.EventSink.Publish(new InteractableConsumedEvent(instance.InstanceId));
        }

        return DomainResult.Success();
    }

    private static bool WithinInteractionRange(ExplorationActorState actor, GridPosition target)
    {
        int dx = Math.Abs(actor.X - target.X);
        int dy = Math.Abs(actor.Y - target.Y);
        return (dx + dy) <= 1;
    }

    /// <summary>
    /// Attempts to unlock the interactable. Returns <c>(success=true, unlocked=true)</c> on
    /// success; <c>(success=true, unlocked=false)</c> when the lock blocks the actor but no
    /// state changes occurred — the <see cref="InteractableLockedEvent"/> is emitted so
    /// callers can react. Hard errors (failed inventory consume) propagate as failures so
    /// the dispatcher rolls back.
    /// </summary>
    private static (DomainResult Result, bool Unlocked) TryUnlock(
        GameState gameState,
        InteractableInstance instance,
        InteractableDefinition definition,
        InteractWithCommand command,
        CommandContext context)
    {
        string? key = definition.RequiredKeyItemId;
        if (string.IsNullOrWhiteSpace(key))
        {
            // Locked at runtime but no key item defined — only an effect can unlock this.
            context.EventSink.Publish(new InteractableLockedEvent(command.InstanceId, command.ActorId, null));
            return (DomainResult.Success(), false);
        }

        if (gameState.InventoryBag.GetTotalQuantity(key!) <= 0)
        {
            context.EventSink.Publish(new InteractableLockedEvent(command.InstanceId, command.ActorId, key));
            return (DomainResult.Success(), false);
        }

        if (definition.ConsumeKeyOnUnlock)
        {
            if (!gameState.InventoryBag.TryConsume(key!, 1, out string? consumeError))
            {
                return (DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    consumeError ?? $"Failed to consume key '{key}'.")), false);
            }

            int remaining = gameState.InventoryBag.GetTotalQuantity(key!);
            context.EventSink.Publish(new InventoryItemChangedEvent(key!, -1, remaining));
        }

        InteractableStatus previousStatus = instance.Status;
        instance.Locked = false;
        instance.Status = InteractableStatus.Unlocked;
        context.EventSink.Publish(new InteractableStatusChangedEvent(instance.InstanceId, previousStatus, instance.Status));
        return (DomainResult.Success(), true);
    }

    private DomainResult ApplyEffect(
        GameState gameState,
        InteractableInstance instance,
        InteractableEffectDefinition effect,
        InteractWithCommand command,
        CommandContext context)
    {
        switch (effect.Kind)
        {
            case InteractableEffectKind.GrantLootTable:
                return _lootHandler.Handle(gameState, new RollAndGrantLootCommand(effect.TargetId), context);

            case InteractableEffectKind.SetWorldBool:
                return _worldHandler.Handle(
                    gameState,
                    new SetWorldVariableCommand(effect.TargetId, WorldVariableValue.FromBool(effect.BoolValue)),
                    context);

            case InteractableEffectKind.SetWorldInt:
                return _worldHandler.Handle(
                    gameState,
                    new SetWorldVariableCommand(effect.TargetId, WorldVariableValue.FromInt(effect.IntValue)),
                    context);

            case InteractableEffectKind.ChangeInteractableStatus:
                return ApplyChangeStatus(gameState, effect, context);

            case InteractableEffectKind.UnlockInteractable:
                return ApplyLockChange(gameState, effect.TargetId, locked: false, context);

            case InteractableEffectKind.LockInteractable:
                return ApplyLockChange(gameState, effect.TargetId, locked: true, context);

            case InteractableEffectKind.EmitInteractionSignal:
                context.EventSink.Publish(new InteractionSignalEvent(effect.TargetId, instance.InstanceId, command.ActorId));
                return DomainResult.Success();

            default:
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    $"Unsupported interactable effect kind '{effect.Kind}'."));
        }
    }

    private static DomainResult ApplyChangeStatus(GameState gameState, InteractableEffectDefinition effect, CommandContext context)
    {
        if (!gameState.InteractablesState.TryGet(effect.TargetId, out InteractableInstance target))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"ChangeInteractableStatus target '{effect.TargetId}' not found."));
        }

        InteractableStatus next = (InteractableStatus)effect.IntValue;
        if (!Enum.IsDefined(typeof(InteractableStatus), next))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                $"Invalid InteractableStatus value {effect.IntValue}."));
        }

        InteractableStatus previous = target.Status;
        if (previous == next)
        {
            return DomainResult.Success();
        }

        target.Status = next;
        context.EventSink.Publish(new InteractableStatusChangedEvent(target.InstanceId, previous, next));
        return DomainResult.Success();
    }

    private static DomainResult ApplyLockChange(GameState gameState, string targetId, bool locked, CommandContext context)
    {
        if (!gameState.InteractablesState.TryGet(targetId, out InteractableInstance target))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Lock/unlock target '{targetId}' not found."));
        }

        target.Locked = locked;
        return DomainResult.Success();
    }
}
