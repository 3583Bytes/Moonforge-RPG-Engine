using Moonforge.Core.Runtime.Random;

namespace Moonforge.Core.Tests;

public sealed class Pcg32RandomSourceTests
{
    [Fact]
    public void Known_Vector_Matches_Pcg32_Reference_Stream()
    {
        Pcg32RandomSource random = new(seed: 42, sequence: 54);

        uint[] expected =
        [
            0xa15c02b7,
            0x7b47f409,
            0xba1d3330,
            0x83d2f293,
            0xbfa4784b
        ];

        foreach (uint expectedValue in expected)
        {
            Assert.Equal(expectedValue, random.NextUInt32());
        }
    }

    [Fact]
    public void Same_Seed_And_Sequence_Produce_Identical_Stream()
    {
        Pcg32RandomSource left = new(seed: 12345, sequence: 54);
        Pcg32RandomSource right = new(seed: 12345, sequence: 54);

        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(left.NextUInt32(), right.NextUInt32());
        }
    }

    [Fact]
    public void Different_Sequence_Produces_Different_Stream()
    {
        Pcg32RandomSource left = new(seed: 12345, sequence: 54);
        Pcg32RandomSource right = new(seed: 12345, sequence: 55);

        Assert.NotEqual(left.NextUInt32(), right.NextUInt32());
    }

    [Fact]
    public void Restore_Resumes_Stream_Exactly_Where_Captured()
    {
        Pcg32RandomSource original = new(seed: 42, sequence: 54);
        for (int i = 0; i < 7; i++)
        {
            original.NextUInt32();
        }

        Pcg32RandomSource restored = Pcg32RandomSource.Restore(original.State, original.Increment);

        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(original.NextUInt32(), restored.NextUInt32());
        }
    }

    [Fact]
    public void Restore_Rejects_Even_Increment()
    {
        Assert.Throws<ArgumentException>(() => Pcg32RandomSource.Restore(state: 123, increment: 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void NextInt_Stays_Within_Range(int maxExclusive)
    {
        Pcg32RandomSource random = new(seed: 9001, sequence: 54);

        for (int i = 0; i < 1000; i++)
        {
            int value = random.NextInt(maxExclusive);
            Assert.InRange(value, 0, maxExclusive - 1);
        }
    }

    [Fact]
    public void NextInt_Of_One_Is_Always_Zero()
    {
        Pcg32RandomSource random = new(seed: 7, sequence: 54);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(0, random.NextInt(1));
        }
    }

    [Fact]
    public void NextDouble_Stays_Strictly_Inside_Unit_Interval()
    {
        Pcg32RandomSource random = new(seed: 1337, sequence: 54);

        for (int i = 0; i < 10_000; i++)
        {
            double value = random.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0, $"NextDouble produced {value}.");
        }
    }

    [Fact]
    public void NextDouble_Is_NextUInt32_Divided_By_Two_Pow_32()
    {
        Pcg32RandomSource doubles = new(seed: 42, sequence: 54);
        Pcg32RandomSource raw = new(seed: 42, sequence: 54);

        for (int i = 0; i < 64; i++)
        {
            Assert.Equal(raw.NextUInt32() / 4294967296.0, doubles.NextDouble());
        }
    }

    [Fact]
    public void BattleRngState_Matches_Pcg32_Algorithm_In_Lockstep()
    {
        // BattleRngState intentionally embeds the same PCG32 algorithm so battle replays and
        // engine-level draws share one statistical contract. Lock the two together.
        Pcg32RandomSource engine = new(seed: 555, sequence: 99);
        Moonforge.Core.Combat.BattleRngState battle = new(seed: 555, sequence: 99);

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(engine.NextInt(100), battle.NextInt(100));
        }

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(engine.NextDouble(), battle.NextDouble());
        }
    }
}
