namespace HiddenHues.Core;

/// <summary>
/// The grid. Every cell has a target color (the hidden picture) and holds either a pixel or nothing.
/// Colors are palette indices; <see cref="Empty"/> marks a cell with no pixel.
/// </summary>
public sealed class Board
{
    public const int Empty = -1;

    private readonly int[] _target;
    private readonly int[] _occupant;

    public Board(int width, int height, int[] target, int[] occupants)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Board must be at least 1x1.");
        if (target.Length != width * height || occupants.Length != width * height)
            throw new ArgumentException("Target and occupant arrays must have Width * Height entries.");

        Width = width;
        Height = height;
        _target = (int[])target.Clone();
        _occupant = (int[])occupants.Clone();
    }

    public int Width { get; }
    public int Height { get; }

    public bool Contains(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

    public int TargetAt(GridPos p) => _target[Index(p)];

    public int OccupantAt(GridPos p) => _occupant[Index(p)];

    public bool IsEmpty(GridPos p) => _occupant[Index(p)] == Empty;

    public bool IsSolved
    {
        get
        {
            for (var i = 0; i < _target.Length; i++)
                if (_occupant[i] != _target[i])
                    return false;
            return true;
        }
    }

    /// <summary>True if some empty cell is waiting for this color.</summary>
    public bool HasOpenSlotFor(int color)
    {
        for (var i = 0; i < _target.Length; i++)
            if (_occupant[i] == Empty && _target[i] == color)
                return true;
        return false;
    }

    internal void SetOccupant(GridPos p, int color) => _occupant[Index(p)] = color;

    private int Index(GridPos p)
    {
        if (!Contains(p))
            throw new ArgumentOutOfRangeException(nameof(p), $"{p} is outside the {Width}x{Height} board.");
        return p.Y * Width + p.X;
    }
}
