using System;
using System.Collections.Generic;

namespace Moonforge.Core.Dialogue
{

    public sealed class DialogueInstanceState
    {
        private readonly HashSet<string> _visitedNodes = new(StringComparer.Ordinal);
        private readonly HashSet<string> _chosenChoices = new(StringComparer.Ordinal);

        public DialogueInstanceState(string dialogueId)
        {
            DialogueId = dialogueId;
        }

        public string DialogueId { get; }

        public string? CurrentNodeId { get; set; }

        public bool Completed { get; set; }

        public IReadOnlyCollection<string> VisitedNodes => _visitedNodes;

        public IReadOnlyCollection<string> ChosenChoices => _chosenChoices;

        public void MarkVisitedNode(string nodeId)
        {
            _visitedNodes.Add(nodeId);
        }

        public void MarkChosenChoice(string choiceId)
        {
            _chosenChoices.Add(choiceId);
        }

        public void CopyFrom(DialogueInstanceState source)
        {
            CurrentNodeId = source.CurrentNodeId;
            Completed = source.Completed;
            _visitedNodes.Clear();
            foreach (string nodeId in source._visitedNodes)
            {
                _visitedNodes.Add(nodeId);
            }

            _chosenChoices.Clear();
            foreach (string choiceId in source._chosenChoices)
            {
                _chosenChoices.Add(choiceId);
            }
        }
    }
}
