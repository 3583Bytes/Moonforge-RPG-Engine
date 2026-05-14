using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Runtime.Commands;

public sealed class CommandContext
{
    public CommandContext(
        IRandomSource randomSource,
        IGameClock clock,
        IFormulaEvaluator formulaEvaluator,
        IDomainEventSink eventSink)
        : this(randomSource, clock, formulaEvaluator, eventSink, EmptyGameDefinitionCatalog.Instance)
    {
    }

    public CommandContext(
        IRandomSource randomSource,
        IGameClock clock,
        IFormulaEvaluator formulaEvaluator,
        IDomainEventSink eventSink,
        IGameDefinitionCatalog definitions)
    {
        RandomSource = randomSource;
        Clock = clock;
        FormulaEvaluator = formulaEvaluator;
        EventSink = eventSink;
        Definitions = definitions;
    }

    public IRandomSource RandomSource { get; }

    public IGameClock Clock { get; }

    public IFormulaEvaluator FormulaEvaluator { get; }

    public IDomainEventSink EventSink { get; }

    public IGameDefinitionCatalog Definitions { get; }

    public CommandContext WithEventSink(IDomainEventSink eventSink)
    {
        return new CommandContext(RandomSource, Clock, FormulaEvaluator, eventSink, Definitions);
    }
}
