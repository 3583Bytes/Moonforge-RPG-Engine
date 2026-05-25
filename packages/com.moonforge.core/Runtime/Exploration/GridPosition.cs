namespace Moonforge.Core.Exploration;

public readonly struct GridPosition
{
    public GridPosition(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}
