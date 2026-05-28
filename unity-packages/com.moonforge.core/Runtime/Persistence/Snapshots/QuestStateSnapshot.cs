using System.Collections.Generic;
using Moonforge.Core.Quests;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class QuestStateSnapshot
    {
        public List<QuestInstanceSnapshot> Quests { get; set; } = new();
    }

    public sealed class QuestInstanceSnapshot
    {
        public string QuestId { get; set; } = string.Empty;

        public QuestStatus Status { get; set; } = QuestStatus.NotStarted;

        public Dictionary<string, int> ObjectiveProgress { get; set; } = new();
    }
}
