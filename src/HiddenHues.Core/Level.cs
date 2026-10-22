namespace HiddenHues.Core;

/// <summary>A hidden picture: a palette and the target color of every cell.</summary>
public sealed class Level
{
    public Level(int width, int height, IReadOnlyList<Rgb> palette, int[] target)
    {
        if (target.Length != width * height)
            throw new ArgumentException("Target must have Width * Height entries.", nameof(target));

        Width = width;
        Height = height;
        Palette = palette;
        Target = target;
    }

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<Rgb> Palette { get; }
    public int[] Target { get; }

    /// <summary>Builds a level from rows of characters, one character per cell, each mapped to a palette color.</summary>
    public static Level FromRows(IReadOnlyList<string> rows, IReadOnlyDictionary<char, Rgb> colors)
    {
        var width = rows[0].Length;
        if (rows.Any(r => r.Length != width))
            throw new ArgumentException("Every row must be the same length.", nameof(rows));

        var keys = colors.Keys.ToList();
        var target = new int[width * rows.Count];
        for (var y = 0; y < rows.Count; y++)
        for (var x = 0; x < width; x++)
        {
            var index = keys.IndexOf(rows[y][x]);
            if (index < 0)
                throw new ArgumentException($"Row {y} uses '{rows[y][x]}', which isn't in the palette.", nameof(rows));
            target[y * width + x] = index;
        }

        return new Level(width, rows.Count, keys.Select(k => colors[k]).ToList(), target);
    }

    /// <summary>A board whose pixels are a random shuffle of the target colors.</summary>
    public Board CreateScrambledBoard(int seed)
    {
        var occupants = (int[])Target.Clone();
        var rng = new Random(seed);
        for (var i = occupants.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (occupants[i], occupants[j]) = (occupants[j], occupants[i]);
        }

        return new Board(Width, Height, Target, occupants);
    }
}
