using System;

namespace Moonforge.Core.Combat
{

    public sealed class BattleRngState
    {
        private ulong _state;
        private readonly ulong _increment;

        public BattleRngState(ulong seed, ulong sequence = 777)
        {
            Sequence = sequence;
            _increment = (sequence << 1) | 1;
            _state = 0;
            NextUInt32();
            _state += seed;
            NextUInt32();
            RollsUsed = 0;
        }

        private BattleRngState(ulong state, ulong sequence, ulong rollsUsed)
        {
            _state = state;
            Sequence = sequence;
            _increment = (sequence << 1) | 1;
            RollsUsed = rollsUsed;
        }

        public ulong Sequence { get; }

        public ulong RollsUsed { get; private set; }

        public uint NextUInt32()
        {
            ulong oldState = _state;
            _state = unchecked(oldState * 6364136223846793005UL + _increment);
            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            RollsUsed++;
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
            // Redraws bump RollsUsed via NextUInt32, keeping replay accounting exact.
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

        public BattleRngState Clone()
        {
            return new BattleRngState(_state, Sequence, RollsUsed);
        }
    }
}
