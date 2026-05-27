using System;
using System.Collections.Generic;

namespace Moonforge.Core.Dialogue
{

    public sealed class DialogueState
    {
        private readonly Dictionary<string, DialogueInstanceState> _dialogues = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, DialogueInstanceState> Dialogues => _dialogues;

        public DialogueInstanceState GetOrCreate(string dialogueId)
        {
            if (_dialogues.TryGetValue(dialogueId, out DialogueInstanceState existing))
            {
                return existing;
            }

            DialogueInstanceState created = new(dialogueId);
            _dialogues[dialogueId] = created;
            return created;
        }

        public bool TryGet(string dialogueId, out DialogueInstanceState instance)
        {
            return _dialogues.TryGetValue(dialogueId, out instance!);
        }

        public void CopyFrom(DialogueState source)
        {
            _dialogues.Clear();
            foreach ((string key, DialogueInstanceState value) in source._dialogues)
            {
                DialogueInstanceState copy = new(key);
                copy.CopyFrom(value);
                _dialogues[key] = copy;
            }
        }
    }
}
