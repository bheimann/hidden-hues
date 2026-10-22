namespace HiddenHues.Core.Generation;

/// <summary>A level with a scrambled top layer that is known to be solvable at its capacity.</summary>
public sealed record Puzzle(Level Level, int[] Occupants, int Capacity, int Seed, IReadOnlyList<GridPos> Solution)
{
    public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(5);

    public Board CreateBoard() => new(Level.Width, Level.Height, Level.Target, Occupants);

    /// <summary>
    /// Shuffles the level until a solvable layout turns up. Each attempt uses the next seed, so the result is
    /// repeatable. Returns null if nothing solvable was found within the time limit (try more capacity, fewer
    /// colors or a smaller grid).
    /// </summary>
    public static Puzzle? Generate(Level level, ShuffleStyle style, int capacity = Game.DefaultCapacity, int seed = 1,
        TimeSpan? timeLimit = null, int attempts = 40, int nodeBudget = 5_000)
    {
        using var timeout = new CancellationTokenSource(timeLimit ?? DefaultTimeLimit);

        for (var attempt = 0; attempt < attempts && !timeout.IsCancellationRequested; attempt++)
        {
            var trySeed = seed + attempt;
            var occupants = Scrambler.Shuffle(level.Width, level.Height, level.Target, style, new Random(trySeed));
            if (occupants.AsSpan().SequenceEqual(level.Target))
                continue;

            var solution = Solver.Solve(level.Width, level.Height, level.Target, occupants, capacity, nodeBudget,
                timeout.Token);
            if (solution is not null
                && Solver.Replay(level.Width, level.Height, level.Target, occupants, capacity, solution))
                return new Puzzle(level, occupants, capacity, trySeed, solution);
        }

        return null;
    }
}
