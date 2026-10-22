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
