using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Loot.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Loot.Commands
{

    public sealed class RollAndGrantLootCommandHandler : ICommandHandler<RollAndGrantLootCommand>
    {
        private readonly ICommandHandler<EconomyTransactionCommand> _transactionHandler;

        /// <summary>
        /// Pass the handler you registered for <see cref="EconomyTransactionCommand"/> so the
        /// transaction composed into a loot grant behaves identically to one dispatched directly.
        /// Defaults to the built-in handler.
        /// </summary>
        public RollAndGrantLootCommandHandler(ICommandHandler<EconomyTransactionCommand>? transactionHandler = null)
        {
            _transactionHandler = transactionHandler ?? new EconomyTransactionCommandHandler();
        }

        public DomainResult Handle(GameState gameState, RollAndGrantLootCommand command, CommandContext context)
        {
            if (!context.Definitions.TryGetLootTable(command.TableId, out LootTableDefinition table))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.NotFound,
                    $"Unknown loot table '{command.TableId}'."));
            }

            LootRollResult result = LootResolver.Roll(gameState, context.Definitions, context.RandomSource, table);
            if (result.IsEmpty)
            {
                context.EventSink.Publish(new LootRolledEvent(command.TableId, 0, 0));
                return DomainResult.Success();
            }

            List<CurrencyDelta> currencyDeltas = new(result.Currencies.Count);
            foreach (LootCurrencyDrop currency in result.Currencies)
            {
                currencyDeltas.Add(new CurrencyDelta(currency.CurrencyId, currency.Amount));
            }

            List<InventoryDelta> inventoryDeltas = new(result.Items.Count);
            foreach (LootDrop item in result.Items)
            {
                inventoryDeltas.Add(new InventoryDelta(item.ItemId, item.Quantity));
            }

            DomainResult txResult = _transactionHandler.Handle(
                gameState,
                new EconomyTransactionCommand(currencyDeltas: currencyDeltas, inventoryDeltas: inventoryDeltas),
                context);
            if (!txResult.IsSuccess)
            {
                return txResult;
            }

            foreach (LootDrop item in result.Items)
            {
                context.EventSink.Publish(new LootItemDroppedEvent(command.TableId, item.ItemId, item.Quantity));
            }

            foreach (LootCurrencyDrop currency in result.Currencies)
            {
                context.EventSink.Publish(new LootCurrencyDroppedEvent(command.TableId, currency.CurrencyId, currency.Amount));
            }

            context.EventSink.Publish(new LootRolledEvent(command.TableId, result.Items.Count, result.Currencies.Count));
            return DomainResult.Success();
        }
    }
}
