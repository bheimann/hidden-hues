namespace HiddenHues.Core.Generation;

public sealed record ReduceOptions
{
    /// <summary>Cells along the picture's longer side. The shorter side follows the picture's shape.</summary>
    public int LongSide { get; init; } = 16;

    /// <summary>The most colors to use. Colors that look nearly the same are merged, so a level can end up with fewer.</summary>
    public int Colors { get; init; } = 6;

    /// <summary>Palette colors closer than this (CIELAB distance) are merged into one.</summary>
    public double MergeDistance { get; init; } = 10;

    /// <summary>Replace lone cells whose neighbors all share another color.</summary>
    public bool Despeckle { get; init; } = true;

    public int Seed { get; init; } = 1;
}

/// <summary>Turns a picture into a level: shrinks it to a small grid, then reduces it to a few distinct colors.</summary>
public static class PictureReducer
{
    public const int MinSide = 4;

    public static Level Reduce(PixelImage image, ReduceOptions options)
    {
        if (options.LongSide < MinSide)
            throw new ArgumentOutOfRangeException(nameof(options), $"The long side must be at least {MinSide} cells.");
        if (options.Colors < 2)
            throw new ArgumentOutOfRangeException(nameof(options), "A level needs at least 2 colors.");

        var (width, height) = GridSize(image.Width, image.Height, options.LongSide);
        var cells = Downscale(image, width, height);
        var (palette, assignment) = KMeans(cells, options.Colors, options.Seed);
        (palette, assignment) = MergeClose(cells, palette, assignment, options.MergeDistance);
        if (options.Despeckle)
            Despeckle(assignment, width, height);

        return BuildLevel(width, height, palette, assignment);
    }

    public static (int Width, int Height) GridSize(int imageWidth, int imageHeight, int longSide)
    {
        var shortSide = (int)Math.Round(longSide * (double)Math.Min(imageWidth, imageHeight) / Math.Max(imageWidth, imageHeight));
        shortSide = Math.Clamp(shortSide, MinSide, longSide);
        return imageWidth >= imageHeight ? (longSide, shortSide) : (shortSide, longSide);
    }

    /// <summary>Averages each block of source pixels into one cell. The picture is center-cropped to the grid's shape first.</summary>
    internal static Lab[] Downscale(PixelImage image, int width, int height)
    {
        // Crop the source so its aspect ratio matches the grid exactly.
        var scale = Math.Min((double)image.Width / width, (double)image.Height / height);
        var cropW = width * scale;
        var cropH = height * scale;
        var left = (image.Width - cropW) / 2;
        var top = (image.Height - cropH) / 2;

        var cells = new Lab[width * height];
        for (var cy = 0; cy < height; cy++)
        for (var cx = 0; cx < width; cx++)
        {
            var x0 = (int)Math.Floor(left + cx * scale);
            var x1 = Math.Max(x0 + 1, (int)Math.Floor(left + (cx + 1) * scale));
            var y0 = (int)Math.Floor(top + cy * scale);
            var y1 = Math.Max(y0 + 1, (int)Math.Floor(top + (cy + 1) * scale));

            double l = 0, a = 0, b = 0;
            var count = 0;
            for (var y = y0; y < Math.Min(y1, image.Height); y++)
            for (var x = x0; x < Math.Min(x1, image.Width); x++)
            {
                var lab = Lab.FromRgb(image.ColorAt(x, y));
                l += lab.L;
                a += lab.A;
                b += lab.B;
                count++;
            }

            cells[cy * width + cx] = new Lab(l / count, a / count, b / count);
        }

        return cells;
    }

    /// <summary>k-means with k-means++ seeding, so the same seed always gives the same palette.</summary>
    internal static (List<Lab> Palette, int[] Assignment) KMeans(Lab[] cells, int k, int seed)
    {
        var rng = new Random(seed);
        k = Math.Min(k, cells.Distinct().Count());

        var centers = new List<Lab> { cells[rng.Next(cells.Length)] };
        var nearest = new double[cells.Length];
        while (centers.Count < k)
        {
            double total = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                nearest[i] = centers.Min(c => c.DistanceSquared(cells[i]));
                total += nearest[i];
            }

            var pick = rng.NextDouble() * total;
            var chosen = cells.Length - 1;
            for (var i = 0; i < cells.Length; i++)
            {
                pick -= nearest[i];
                if (pick <= 0)
                {
                    chosen = i;
                    break;
                }
            }

            centers.Add(cells[chosen]);
        }

        var assignment = new int[cells.Length];
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var changed = Assign(cells, centers, assignment);
            centers = Recenter(cells, centers, assignment);
            if (!changed && iteration > 0)
                break;
        }

        return (centers, assignment);
    }

    /// <summary>Merges palette colors that look nearly the same, closest pair first.</summary>
    internal static (List<Lab> Palette, int[] Assignment) MergeClose(
        Lab[] cells, List<Lab> palette, int[] assignment, double mergeDistance)
    {
        var limit = mergeDistance * mergeDistance;
        while (palette.Count > 2)
        {
            var (bestA, bestB, best) = (-1, -1, double.MaxValue);
            for (var i = 0; i < palette.Count; i++)
            for (var j = i + 1; j < palette.Count; j++)
            {
                var d = palette[i].DistanceSquared(palette[j]);
                if (d < best)
                    (bestA, bestB, best) = (i, j, d);
            }

            if (best >= limit)
                break;

            for (var i = 0; i < assignment.Length; i++)
            {
                if (assignment[i] == bestB) assignment[i] = bestA;
                else if (assignment[i] > bestB) assignment[i]--;
            }

            palette.RemoveAt(bestB);
            palette = Recenter(cells, palette, assignment);
        }

        return (palette, assignment);
    }

    /// <summary>A cell whose neighbors (at least two of them) all share one other color takes that color.</summary>
    internal static void Despeckle(int[] assignment, int width, int height)
    {
        var source = (int[])assignment.Clone();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var neighbors = new List<int>(4);
            if (y > 0) neighbors.Add(source[(y - 1) * width + x]);
            if (x < width - 1) neighbors.Add(source[y * width + x + 1]);
            if (y < height - 1) neighbors.Add(source[(y + 1) * width + x]);
            if (x > 0) neighbors.Add(source[y * width + x - 1]);

            var self = source[y * width + x];
            if (neighbors.Count >= 2 && neighbors.All(n => n == neighbors[0]) && neighbors[0] != self)
                assignment[y * width + x] = neighbors[0];
        }
    }

    private static bool Assign(Lab[] cells, List<Lab> centers, int[] assignment)
    {
        var changed = false;
        for (var i = 0; i < cells.Length; i++)
        {
            var best = 0;
            for (var c = 1; c < centers.Count; c++)
                if (centers[c].DistanceSquared(cells[i]) < centers[best].DistanceSquared(cells[i]))
                    best = c;

            changed |= assignment[i] != best;
            assignment[i] = best;
        }

        return changed;
    }

    private static List<Lab> Recenter(Lab[] cells, List<Lab> centers, int[] assignment)
    {
        var sums = new (double L, double A, double B, int N)[centers.Count];
        for (var i = 0; i < cells.Length; i++)
        {
            ref var s = ref sums[assignment[i]];
            s = (s.L + cells[i].L, s.A + cells[i].A, s.B + cells[i].B, s.N + 1);
        }

        // An empty cluster keeps its old center; it's dropped when the level is built.
        return sums.Select((s, c) => s.N == 0 ? centers[c] : new Lab(s.L / s.N, s.A / s.N, s.B / s.N)).ToList();
    }

    /// <summary>Drops unused colors and orders the palette dark to light, so the same picture always gives the same level.</summary>
    private static Level BuildLevel(int width, int height, List<Lab> palette, int[] assignment)
    {
        var used = assignment.Distinct().OrderBy(c => palette[c].L).ToList();
        var remap = used.Select((old, index) => (old, index)).ToDictionary(p => p.old, p => p.index);
        var target = assignment.Select(c => remap[c]).ToArray();
        return new Level(width, height, used.Select(c => palette[c].ToRgb()).ToList(), target);
    }
}
