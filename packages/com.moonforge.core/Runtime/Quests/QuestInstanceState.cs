using System;
using System.Collections.Generic;

namespace Moonforge.Core.Quests
{

    public sealed class QuestInstanceState
    {
        private readonly Dictionary<string, int> _objectiveProgress = new(StringComparer.Ordinal);

        public QuestInstanceState(string questId)
        {
            QuestId = questId;
            Status = QuestStatus.NotStarted;
        }

        public string QuestId { get; }

        public QuestStatus Status { get; set; }

        public IReadOnlyDictionary<string, int> ObjectiveProgress => _objectiveProgress;

        public int GetObjectiveProgress(string objectiveId)
        {
            return _objectiveProgress.TryGetValue(objectiveId, out int value) ? value : 0;
        }

        public void SetObjectiveProgress(string objectiveId, int value)
        {
            _objectiveProgress[objectiveId] = value;
        }

        public void ClearObjectiveProgress()
        {
            _objectiveProgress.Clear();
        }

        public void CopyFrom(QuestInstanceState source)
        {
            Status = source.Status;
            _objectiveProgress.Clear();
            foreach ((string key, int value) in source._objectiveProgress)
            {
                _objectiveProgress[key] = value;
            }
        }
    }
}
