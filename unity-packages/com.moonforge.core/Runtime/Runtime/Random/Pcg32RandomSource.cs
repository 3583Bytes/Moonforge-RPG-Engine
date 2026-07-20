using System;

namespace Moonforge.Core.Runtime.Random
{

    /// <summary>
    /// PCG32 implementation pinned for deterministic streams.
    /// </summary>
    public sealed class Pcg32RandomSource : IRandomSource
    {
        private ulong _state;
        private ulong _increment;

        public Pcg32RandomSource(ulong seed, ulong sequence = 54)
        {
            _increment = (sequence << 1) | 1;
            _state = 0;
            NextUInt32();
            _state += seed;
            NextUInt32();
        }

        private Pcg32RandomSource()
        {
            // Used by Restore to rebuild an exact stream position without re-seeding.
        }

        /// <summary>
        /// Raw PCG32 engine state at the current stream position. Persist together with
        /// <see cref="Increment"/> and feed both to <see cref="Restore"/> to resume the
        /// stream exactly where it left off (e.g. across save/load).
        /// </summary>
        public ulong State => _state;

        /// <summary>
        /// Odd stream-selection increment derived from the constructor's sequence parameter.
        /// </summary>
        public ulong Increment => _increment;

        /// <summary>
        /// Rebuilds a source at the exact stream position previously captured from
        /// <see cref="State"/> and <see cref="Increment"/>. The restored source produces
        /// the same continuation of the stream as the captured one would have.
        /// </summary>
        public static Pcg32RandomSource Restore(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0UL)
            {
                throw new ArgumentException("PCG32 increment must be odd.", nameof(increment));
            }

            Pcg32RandomSource restored = new();
            restored._state = state;
            restored._increment = increment;
            return restored;
        }

        public uint NextUInt32()
        {
            ulong oldState = _state;
            _state = unchecked(oldState * 6364136223846793005UL + _increment);
            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            // Rejection sampling (the PCG reference "bounded rand"): redraw the rare values
            // below 2^32 mod bound so every result in [0, bound) is exactly equally likely —
            // a plain modulo slightly favors low results for non-power-of-two bounds.
            uint bound = (uint)maxExclusive;
            uint threshold = unchecked(0u - bound) % bound;
            uint value = NextUInt32();
            while (value < threshold)
            {
                value = NextUInt32();
            }

            return (int)(value % bound);
        }

        public double NextDouble()
        {
            // Divide by 2^32 (not uint.MaxValue) so the result stays strictly inside [0, 1).
            return NextUInt32() / 4294967296.0;
        }
    }
}
