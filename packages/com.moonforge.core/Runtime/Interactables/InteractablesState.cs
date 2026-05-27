using System;
using System.Collections.Generic;
using Moonforge.Core.Exploration;

namespace Moonforge.Core.Interactables
{

    public sealed class InteractablesState
    {
        private readonly Dictionary<string, InteractableInstance> _instances = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, InteractableInstance> Instances => _instances;

        public bool TryGet(string instanceId, out InteractableInstance instance)
        {
            return _instances.TryGetValue(instanceId, out instance!);
        }

        public InteractableInstance? FindAt(GridPosition position)
        {
            foreach (KeyValuePair<string, InteractableInstance> pair in _instances)
            {
                GridPosition p = pair.Value.Position;
                if (p.X == position.X && p.Y == position.Y)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        public void Add(InteractableInstance instance)
        {
            if (string.IsNullOrWhiteSpace(instance.InstanceId))
            {
                throw new ArgumentException("Instance ID is required.", nameof(instance));
            }

            _instances[instance.InstanceId] = instance;
        }

        public bool Remove(string instanceId)
        {
            return _instances.Remove(instanceId);
        }

        public void CopyFrom(InteractablesState source)
        {
            _instances.Clear();
            foreach (KeyValuePair<string, InteractableInstance> pair in source._instances)
            {
                _instances[pair.Key] = pair.Value.Clone();
            }
        }
    }
}
