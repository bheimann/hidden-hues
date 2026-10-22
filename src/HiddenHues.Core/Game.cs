namespace HiddenHues.Core;

public enum GameStatus
{
    Playing,
    Won,
    Lost,
}

/// <summary>A pixel riding the belt. The id lets the renderer follow it between ticks.</summary>
public readonly record struct BeltPixel(int Id, int Color);

/// <summary>A pixel that dropped off the belt into a cell during a tick.</summary>
public readonly record struct Placement(BeltPixel Pixel, int Tile, GridPos Cell);

/// <summary>
/// The tick-based simulation. Picking takes a region off the board into the feeder; each tick the belt
/// advances one tile, the feeder releases one pixel, and pixels drop into matching cells they pass.
/// </summary>
public sealed class Game
{
    public const int DefaultCapacity = 30;

    private readonly BeltPixel?[] _belt;
    private readonly Queue<int> _feeder = new();
    private int _nextId;

    public Game(Board board, int capacity = DefaultCapacity, int? feederColumn = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");

        Board = board;
        Capacity = capacity;
        Layout = new BeltLayout(board.Width, board.Height, feederColumn ?? board.Width / 2);
        _belt = new BeltPixel?[Layout.Length];
        UpdateStatus();
    }

    public Board Board { get; }
    public BeltLayout Layout { get; }
    public int Capacity { get; }
    public GameStatus Status { get; private set; }

    /// <summary>The pixel on each belt tile, indexed like <see cref="BeltLayout.Tiles"/>.</summary>
    public IReadOnlyList<BeltPixel?> Belt => _belt;

    public int FeederCount => _feeder.Count;

    /// <summary>Pixels in the feeder plus pixels on the belt. This is what capacity limits.</summary>
    public int InTransit => _feeder.Count + _belt.Count(p => p is not null);

    public int Room => Capacity - InTransit;

    /// <summary>
    /// Takes the clicked pixel and every same-colored pixel connected to it, nearest first,
    /// until the region runs out or there's no more room. Returns the cells taken.
    /// </summary>
    public IReadOnlyList<GridPos> Pick(GridPos cell)
    {
        if (Status != GameStatus.Playing || !Board.Contains(cell) || Board.IsEmpty(cell) || Room <= 0)
            return Array.Empty<GridPos>();

        var color = Board.OccupantAt(cell);
        var taken = new List<GridPos>();
        var seen = new HashSet<GridPos> { cell };
        var queue = new Queue<GridPos>();
        queue.Enqueue(cell);
        var room = Room;

        while (queue.Count > 0 && taken.Count < room)
        {
            var c = queue.Dequeue();
            taken.Add(c);
            foreach (var n in Neighbors(c))
                if (Board.Contains(n) && Board.OccupantAt(n) == color && seen.Add(n))
                    queue.Enqueue(n);
        }

        foreach (var c in taken)
        {
            Board.SetOccupant(c, Board.Empty);
            _feeder.Enqueue(color);
        }

        UpdateStatus();
        return taken;
    }

    /// <summary>Advances the belt one tile, releases a pixel from the feeder, and drops pixels into matching cells.</summary>
    public IReadOnlyList<Placement> Tick()
    {
        if (Status != GameStatus.Playing)
            return Array.Empty<Placement>();

        var last = _belt[^1];
        Array.Copy(_belt, 0, _belt, 1, _belt.Length - 1);
        _belt[0] = last;

        if (_belt[0] is null && _feeder.Count > 0)
            _belt[0] = new BeltPixel(_nextId++, _feeder.Dequeue());

        var placements = new List<Placement>();
        for (var tile = 0; tile < _belt.Length; tile++)
        {
            if (_belt[tile] is not { } pixel)
                continue;

            foreach (var cell in Layout.HomesAt(tile))
            {
                if (Board.IsEmpty(cell) && Board.TargetAt(cell) == pixel.Color)
                {
                    Board.SetOccupant(cell, pixel.Color);
                    _belt[tile] = null;
                    placements.Add(new Placement(pixel, tile, cell));
                    break;
                }
            }
        }

        UpdateStatus();
        return placements;
    }

    private void UpdateStatus()
    {
        if (Board.IsSolved)
        {
            Status = GameStatus.Won;
            return;
        }

        // Full, and nothing in transit has anywhere to go: nothing can drop, and there's no room to pick.
        if (Room <= 0 && !InTransitColors().Any(Board.HasOpenSlotFor))
            Status = GameStatus.Lost;
    }

    private IEnumerable<int> InTransitColors() =>
        _feeder.Concat(_belt.Where(p => p is not null).Select(p => p!.Value.Color)).Distinct();

    private static IEnumerable<GridPos> Neighbors(GridPos c)
    {
        yield return c with { Y = c.Y - 1 };
        yield return c with { X = c.X + 1 };
        yield return c with { Y = c.Y + 1 };
        yield return c with { X = c.X - 1 };
    }
}
