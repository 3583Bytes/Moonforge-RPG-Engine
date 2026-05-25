using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Quests.Commands;

public sealed class EmitQuestSignalCommand : ICommand
{
    public EmitQuestSignalCommand(QuestSignalType signalType, string targetId, int amount = 1)
    {
        SignalType = signalType;
        TargetId = targetId;
        Amount = amount;
    }

    public QuestSignalType SignalType { get; }

    public string TargetId { get; }

    public int Amount { get; }
}
