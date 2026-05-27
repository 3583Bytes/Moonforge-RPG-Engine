using System;

namespace Moonforge.Core.Runtime.Random
{

    /// <summary>
    /// PCG32 implementation pinned for deterministic streams.
    /// </summary>
    public sealed class Pcg32RandomSource : IRandomSource
    {
        private ulong _state;
        private readonly ulong _increment;

        public Pcg32RandomSource(ulong seed, ulong sequence = 54)
        {
            _increment = (sequence << 1) | 1;
            _state = 0;
            NextUInt32();
            _state += seed;
            NextUInt32();
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

            return (int)(NextUInt32() % (uint)maxExclusive);
        }

        public double NextDouble()
        {
            return NextUInt32() / (double)uint.MaxValue;
        }
    }
}
