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

            return (int)(NextUInt32() % (uint)maxExclusive);
        }

        public double NextDouble()
        {
            return NextUInt32() / (double)uint.MaxValue;
        }

        public BattleRngState Clone()
        {
            return new BattleRngState(_state, Sequence, RollsUsed);
        }
    }
}
