using System;
using System.Collections.Generic;

namespace Moonforge.Core.Quests
{

    public sealed class QuestState
    {
        private readonly Dictionary<string, QuestInstanceState> _quests = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, QuestInstanceState> Quests => _quests;

        public QuestInstanceState GetOrCreate(string questId)
        {
            if (_quests.TryGetValue(questId, out QuestInstanceState existing))
            {
                return existing;
            }

            QuestInstanceState created = new(questId);
            _quests[questId] = created;
            return created;
        }

        public bool TryGet(string questId, out QuestInstanceState quest)
        {
            return _quests.TryGetValue(questId, out quest!);
        }

        public void CopyFrom(QuestState source)
        {
            _quests.Clear();
            foreach ((string key, QuestInstanceState value) in source._quests)
            {
                QuestInstanceState copy = new(key);
                copy.CopyFrom(value);
                _quests[key] = copy;
            }
        }
    }
}
