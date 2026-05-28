using System;
using System.Collections.Generic;

namespace Moonforge.Core.Bestiary
{

    /// <summary>
    /// "Pokédex"-style codex: per-species encounter and capture history. Auto-tracked by the
    /// built-in <see cref="Reactors.BestiaryAutoTrackReactor"/> when a battle starts (encounter)
    /// or a capture event fires; games may also mark entries manually via
    /// <see cref="Commands.MarkSpeciesObservedCommand"/>.
    /// </summary>
    public sealed class BestiaryState
    {
        private readonly Dictionary<string, BestiaryEntry> _entries = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, BestiaryEntry> Entries => _entries;

        public bool TryGet(string speciesId, out BestiaryEntry entry)
        {
            return _entries.TryGetValue(speciesId, out entry!);
        }

        public int EncounteredSpeciesCount
        {
            get
            {
                int count = 0;
                foreach (BestiaryEntry e in _entries.Values)
                {
                    if (e.IsEncountered)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int CapturedSpeciesCount
        {
            get
            {
                int count = 0;
                foreach (BestiaryEntry e in _entries.Values)
                {
                    if (e.IsCaptured)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Records one encounter with this species. Increments <see cref="BestiaryEntry.EncounterCount"/>
        /// and sets <see cref="BestiaryEntry.FirstEncounteredAtMinutes"/> on the first encounter.
        /// Returns true if this was the first time the species was encountered.
        /// </summary>
        public bool RecordEncounter(string speciesId, long atMinutes)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                return false;
            }

            BestiaryEntry entry = GetOrAdd(speciesId);
            bool first = !entry.IsEncountered;
            entry.EncounterCount++;
            if (first)
            {
                entry.FirstEncounteredAtMinutes = atMinutes;
            }

            return first;
        }

        /// <summary>
        /// Records one capture of this species. Increments <see cref="BestiaryEntry.CaptureCount"/>
        /// and sets <see cref="BestiaryEntry.FirstCapturedAtMinutes"/> on the first capture.
        /// Returns true if this was the first time the species was captured.
        /// </summary>
        public bool RecordCapture(string speciesId, long atMinutes)
        {
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                return false;
            }

            BestiaryEntry entry = GetOrAdd(speciesId);
            bool first = !entry.IsCaptured;
            entry.CaptureCount++;
            if (first)
            {
                entry.FirstCapturedAtMinutes = atMinutes;
            }

            return first;
        }

        public void CopyFrom(BestiaryState source)
        {
            _entries.Clear();
            foreach (KeyValuePair<string, BestiaryEntry> pair in source._entries)
            {
                _entries[pair.Key] = pair.Value.Clone();
            }
        }

        private BestiaryEntry GetOrAdd(string speciesId)
        {
            if (!_entries.TryGetValue(speciesId, out BestiaryEntry? entry))
            {
                entry = new BestiaryEntry(speciesId);
                _entries[speciesId] = entry;
            }

            return entry;
        }
    }
}
