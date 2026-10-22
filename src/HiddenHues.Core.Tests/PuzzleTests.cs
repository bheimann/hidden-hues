using System.Diagnostics;
using HiddenHues.Core;
using HiddenHues.Core.Generation;
using Xunit.Abstractions;

namespace HiddenHues.Core.Tests;

public class PuzzleTests
{
    private readonly ITestOutputHelper _output;

    public PuzzleTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(ShuffleStyle.Clumps)]
    [InlineData(ShuffleStyle.Random)]
    public void Shuffle_KeepsEveryColorsCount(ShuffleStyle style)
    {
        var target = PrototypeLevel.Create().Target;

        var occupants = Scrambler.Shuffle(11, 14, target, style, new Random(7));

        Assert.Equal(target.OrderBy(c => c), occupants.OrderBy(c => c));
    }

    [Fact]
    public void Clumps_MakeBiggerRegionsThanARandomShuffle()
    {
        var target = PrototypeLevel.Create().Target;

        var clumps = AverageRegionSize(Scrambler.Clumps(11, 14, target, new Random(3)), 11, 14);
        var random = AverageRegionSize(Scrambler.Random(target, new Random(3)), 11, 14);

        _output.WriteLine($"average region size: clumps {clumps:F1}, random {random:F1}");
        Assert.True(clumps > random * 1.5, $"clumps {clumps:F1} vs random {random:F1}");
    }

    [Fact]
    public void Solver_FindsAWin_ThatTheRealGameConfirms()
    {
        var target = new[] { 0, 1, 1, 0, 2, 2, 0, 1, 2 };
        var occupants = new[] { 2, 0, 1, 1, 0, 2, 2, 0, 1 };

        var solution = Solver.Solve(3, 3, target, occupants, capacity: 4);

        Assert.NotNull(solution);
        Assert.True(Solver.Replay(3, 3, target, occupants, 4, solution!));
    }

    [Fact]
    public void Solver_ReportsAnUnsolvableBoard()
    {
        // Each pick sends out a pixel whose only slot is still covered, and a capacity of 1 leaves no room for another.
        var target = new[] { 0, 1, 1, 0 };
        var occupants = new[] { 1, 0, 0, 1 };

        Assert.Null(Solver.Solve(2, 2, target, occupants, capacity: 1));
    }

    [Fact]
    public void Generate_MakesASolvableClumpedPrototype()
    {
        var level = PrototypeLevel.Create();
        var timer = Stopwatch.StartNew();

        var puzzle = Puzzle.Generate(level, ShuffleStyle.Clumps, capacity: 30, seed: 1);

        _output.WriteLine($"generated in {timer.ElapsedMilliseconds} ms, seed {puzzle?.Seed}, {puzzle?.Solution.Count} clicks");
        Assert.NotNull(puzzle);
        Assert.True(Solver.Replay(level.Width, level.Height, level.Target, puzzle!.Occupants, 30, puzzle.Solution));
    }

    [Theory]
    [InlineData(32, 12, 30)]
    [InlineData(32, 12, 10)]
    [InlineData(24, 8, 10)]
    public void Generate_LargestGrids_FinishQuickly(int longSide, int colors, int capacity)
    {
        // A busy picture: diagonal bands crossed with rings, so every color appears in many places.
        var rgba = new byte[256 * 256 * 4];
        for (var y = 0; y < 256; y++)
        for (var x = 0; x < 256; x++)
        {
            var i = (y * 256 + x) * 4;
            var ring = (int)Math.Sqrt((x - 128) * (x - 128) + (y - 128) * (y - 128)) / 20;
            (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) =
                ((byte)((x + y) / 2), (byte)(ring * 40 % 256), (byte)(255 - x), 255);
        }

        var level = PictureReducer.Reduce(new PixelImage(256, 256, rgba), new ReduceOptions { LongSide = longSide, Colors = colors });
        var timer = Stopwatch.StartNew();

        var puzzle = Puzzle.Generate(level, ShuffleStyle.Clumps, capacity);

        _output.WriteLine($"{level.Width}x{level.Height}, {level.Palette.Count} colors, capacity {capacity}: " +
                          $"{timer.ElapsedMilliseconds} ms, solved={puzzle is not null}, seed {puzzle?.Seed}");
        // Within the default time limit, plus a little for the node in progress when time runs out.
        Assert.True(timer.Elapsed < Puzzle.DefaultTimeLimit + TimeSpan.FromSeconds(1), $"took {timer.ElapsedMilliseconds} ms");
    }

    private static double AverageRegionSize(int[] grid, int width, int height)
    {
        var visited = new bool[grid.Length];
        var regions = 0;
        for (var start = 0; start < grid.Length; start++)
        {
            if (visited[start])
                continue;
            regions++;
            var stack = new Stack<int>(new[] { start });
            visited[start] = true;
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                var (x, y) = (c % width, c / width);
                foreach (var n in new[] { y > 0 ? c - width : -1, x < width - 1 ? c + 1 : -1, y < height - 1 ? c + width : -1, x > 0 ? c - 1 : -1 })
                    if (n >= 0 && !visited[n] && grid[n] == grid[c])
                    {
                        visited[n] = true;
                        stack.Push(n);
                    }
            }
        }

        return (double)grid.Length / regions;
    }
}
