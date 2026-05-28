namespace Moonforge.Core.Runtime.Random
{

    public interface IRandomSource
    {
        uint NextUInt32();

        int NextInt(int maxExclusive);

        double NextDouble();
    }
}
