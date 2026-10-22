namespace HiddenHues.Core;

/// <summary>A position in grid coordinates. Belt tiles sit just outside the grid (x = -1 or Width, y = -1 or Height).</summary>
public readonly record struct GridPos(int X, int Y)
{
    public override string ToString() => $"({X},{Y})";
}
