namespace HiddenHues.Core.Generation;

public enum ShuffleStyle
{
    /// <summary>Same-colored pixels are grouped into clumps, so picks take several pixels at once.</summary>
    Clumps,

    /// <summary>Every pixel is shuffled on its own. Most picks take a single pixel.</summary>
    Random,
}

/// <summary>Builds the scrambled top layer: the target's pixels, rearranged. Color counts always match the target.</summary>
public static class Scrambler
{
    public static int[] Shuffle(int width, int height, int[] target, ShuffleStyle style, Random rng) => style switch
    {
        ShuffleStyle.Clumps => Clumps(width, height, target, rng),
        ShuffleStyle.Random => Random(target, rng),
        _ => throw new ArgumentOutOfRangeException(nameof(style)),
    };

    public static int[] Random(int[] target, Random rng)
    {
        var occupants = (int[])target.Clone();
        for (var i = occupants.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (occupants[i], occupants[j]) = (occupants[j], occupants[i]);
        }

        return occupants;
    }

    /// <summary>
    /// Grows clumps from random seed cells. Each clump takes a color (weighted by how many of that color are
    /// still unplaced) and spreads into free neighbors until it reaches a random size.
    /// </summary>
    public static int[] Clumps(int width, int height, int[] target, Random rng, int minClump = 3, int maxClump = 10)
    {
        var colors = target.Max() + 1;
        var remaining = new int[colors];
        foreach (var c in target)
            remaining[c]++;

        var occupants = Enumerable.Repeat(Board.Empty, target.Length).ToArray();
        var unplaced = target.Length;

        foreach (var seed in Random(Enumerable.Range(0, target.Length).ToArray(), rng))
        {
            if (occupants[seed] != Board.Empty)
                continue;

            var color = WeightedPick(remaining, unplaced, rng);
            var size = Math.Min(rng.Next(minClump, maxClump + 1), remaining[color]);

            var frontier = new List<int> { seed };
            var placed = 0;
            while (placed < size && frontier.Count > 0)
            {
                var pick = rng.Next(frontier.Count);
                var cell = frontier[pick];
                frontier[pick] = frontier[^1];
                frontier.RemoveAt(frontier.Count - 1);
                if (occupants[cell] != Board.Empty)
                    continue;

                occupants[cell] = color;
                placed++;
                var (x, y) = (cell % width, cell / width);
                if (y > 0) frontier.Add(cell - width);
                if (x < width - 1) frontier.Add(cell + 1);
                if (y < height - 1) frontier.Add(cell + width);
                if (x > 0) frontier.Add(cell - 1);
            }

            remaining[color] -= placed;
            unplaced -= placed;
        }

        return occupants;
    }

    private static int WeightedPick(int[] remaining, int total, Random rng)
    {
        var pick = rng.Next(total);
        for (var c = 0; c < remaining.Length; c++)
        {
            pick -= remaining[c];
            if (pick < 0)
                return c;
        }

        throw new InvalidOperationException("No pixels left to place.");
    }
}
