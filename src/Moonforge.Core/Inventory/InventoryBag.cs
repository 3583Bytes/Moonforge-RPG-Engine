using System;
using System.Collections.Generic;

namespace Moonforge.Core.Inventory;

public sealed class InventoryBag
{
    private readonly List<InventoryStack> _stacks = new();

    public int CapacitySlots { get; private set; } = 32;

    public IReadOnlyList<InventoryStack> Stacks => _stacks;

    public int UsedSlots => _stacks.Count;

    public void SetCapacity(int capacitySlots)
    {
        if (capacitySlots <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacitySlots));
        }

        if (capacitySlots < UsedSlots)
        {
            throw new InvalidOperationException("Cannot set capacity below current used slots.");
        }

        CapacitySlots = capacitySlots;
    }

    public int GetTotalQuantity(string itemId)
    {
        int total = 0;
        foreach (InventoryStack stack in _stacks)
        {
            if (stack.ItemId == itemId)
            {
                total += stack.Quantity;
            }
        }

        return total;
    }

    public bool TryAdd(string itemId, int quantity, int stackLimit, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            error = "Item ID is required.";
            return false;
        }

        if (quantity <= 0)
        {
            error = "Quantity must be positive.";
            return false;
        }

        if (stackLimit <= 0)
        {
            error = "Stack limit must be positive.";
            return false;
        }

        int existingSpace = 0;
        foreach (InventoryStack stack in _stacks)
        {
            if (stack.ItemId != itemId)
            {
                continue;
            }

            if (stack.StackLimit != stackLimit)
            {
                error = $"Stack limit mismatch for '{itemId}'. Existing={stack.StackLimit}, requested={stackLimit}.";
                return false;
            }

            existingSpace += stackLimit - stack.Quantity;
        }

        int remainingAfterExisting = Math.Max(0, quantity - existingSpace);
        int requiredNewStacks = remainingAfterExisting == 0 ? 0 : (remainingAfterExisting + stackLimit - 1) / stackLimit;
        if (UsedSlots + requiredNewStacks > CapacitySlots)
        {
            error = "Not enough inventory slots available.";
            return false;
        }

        int remaining = quantity;
        foreach (InventoryStack stack in _stacks)
        {
            if (stack.ItemId != itemId || stack.Quantity >= stackLimit)
            {
                continue;
            }

            int canFill = stackLimit - stack.Quantity;
            int toMove = Math.Min(canFill, remaining);
            stack.Quantity += toMove;
            remaining -= toMove;

            if (remaining == 0)
            {
                return true;
            }
        }

        while (remaining > 0)
        {
            int toCreate = Math.Min(stackLimit, remaining);
            _stacks.Add(new InventoryStack(itemId, toCreate, stackLimit));
            remaining -= toCreate;
        }

        return true;
    }

    public bool TryConsume(string itemId, int quantity, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            error = "Item ID is required.";
            return false;
        }

        if (quantity <= 0)
        {
            error = "Quantity must be positive.";
            return false;
        }

        int available = GetTotalQuantity(itemId);
        if (available < quantity)
        {
            error = $"Insufficient item quantity for '{itemId}'. Requested={quantity}, available={available}.";
            return false;
        }

        int remaining = quantity;
        for (int i = 0; i < _stacks.Count && remaining > 0; i++)
        {
            InventoryStack stack = _stacks[i];
            if (stack.ItemId != itemId)
            {
                continue;
            }

            int toTake = Math.Min(stack.Quantity, remaining);
            stack.Quantity -= toTake;
            remaining -= toTake;
        }

        _stacks.RemoveAll(stack => stack.Quantity == 0);
        return true;
    }

    public void CopyFrom(InventoryBag source)
    {
        CapacitySlots = source.CapacitySlots;
        _stacks.Clear();
        foreach (InventoryStack stack in source._stacks)
        {
            _stacks.Add(new InventoryStack(stack.ItemId, stack.Quantity, stack.StackLimit));
        }
    }
}
