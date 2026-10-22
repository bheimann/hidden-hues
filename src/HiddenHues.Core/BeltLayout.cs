namespace HiddenHues.Core;

/// <summary>
/// The loop of belt tiles around the grid, running clockwise and starting at the feeder's entry tile.
/// Also maps each cell to its home tiles: the tile directly across from it on its nearest side(s).
/// </summary>
public sealed class BeltLayout
{
    private readonly GridPos[] _tiles;
    private readonly Dictionary<GridPos, int> _indexOf = new();
    private readonly List<GridPos>[] _homesAt;
    private readonly Dictionary<GridPos, List<int>> _homeTilesOf = new();

    public BeltLayout(int width, int height, int feederColumn)
    {
        if (feederColumn < 0 || feederColumn >= width)
            throw new ArgumentOutOfRangeException(nameof(feederColumn), "The feeder must sit above a grid column.");

        var ring = new List<GridPos>(2 * width + 2 * height + 4);
        for (var x = -1; x <= width; x++) ring.Add(new GridPos(x, -1));      // top, left to right
        for (var y = 0; y < height; y++) ring.Add(new GridPos(width, y));    // right, top to bottom
        for (var x = width; x >= -1; x--) ring.Add(new GridPos(x, height));  // bottom, right to left
        for (var y = height - 1; y >= 0; y--) ring.Add(new GridPos(-1, y));  // left, bottom to top

        var start = ring.IndexOf(new GridPos(feederColumn, -1));
        _tiles = ring.Skip(start).Concat(ring.Take(start)).ToArray();
        for (var i = 0; i < _tiles.Length; i++)
            _indexOf[_tiles[i]] = i;

        _homesAt = new List<GridPos>[_tiles.Length];
        for (var i = 0; i < _tiles.Length; i++)
            _homesAt[i] = new List<GridPos>();

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var cell = new GridPos(x, y);
            var top = y;
            var bottom = height - 1 - y;
            var left = x;
            var right = width - 1 - x;
            var nearest = Math.Min(Math.Min(top, bottom), Math.Min(left, right));

            var tiles = new List<int>();
            if (top == nearest) tiles.Add(_indexOf[new GridPos(x, -1)]);
            if (right == nearest) tiles.Add(_indexOf[new GridPos(width, y)]);
            if (bottom == nearest) tiles.Add(_indexOf[new GridPos(x, height)]);
            if (left == nearest) tiles.Add(_indexOf[new GridPos(-1, y)]);

            tiles.Sort();
            _homeTilesOf[cell] = tiles;
            foreach (var t in tiles)
                _homesAt[t].Add(cell);
        }

        // Closest cell first. Every cell a tile serves is a different distance from it.
        foreach (var homes in _homesAt)
            homes.Sort((a, b) => Distance(a).CompareTo(Distance(b)));

        int Distance(GridPos c) => Math.Min(Math.Min(c.Y, height - 1 - c.Y), Math.Min(c.X, width - 1 - c.X));
    }

    public int Length => _tiles.Length;

    /// <summary>Tile positions in travel order. Index 0 is where the feeder drops pixels on.</summary>
    public IReadOnlyList<GridPos> Tiles => _tiles;

    /// <summary>The cells this tile can fill, closest first.</summary>
    public IReadOnlyList<GridPos> HomesAt(int tile) => _homesAt[tile];

    /// <summary>The tiles that can fill this cell, in travel order from the feeder.</summary>
    public IReadOnlyList<int> HomeTilesOf(GridPos cell) => _homeTilesOf[cell];
}
