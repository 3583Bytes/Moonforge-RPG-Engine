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
}
