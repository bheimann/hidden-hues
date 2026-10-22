namespace HiddenHues.Core.Generation;

/// <summary>A level with a scrambled top layer that is known to be solvable at its capacity.</summary>
public sealed record Puzzle(Level Level, int[] Occupants, int Capacity, int Seed, IReadOnlyList<GridPos> Solution)
{
    public Board CreateBoard() => new(Level.Width, Level.Height, Level.Target, Occupants);

    /// <summary>
    /// Shuffles the level until a solvable layout turns up. Each attempt uses the next seed, so the result is
    /// repeatable. Returns null if none of the attempts could be solved (try more capacity or fewer cells).
    /// </summary>
    public static Puzzle? Generate(Level level, ShuffleStyle style, int capacity = Game.DefaultCapacity, int seed = 1,
        int attempts = 40, int nodeBudget = 20_000)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var trySeed = seed + attempt;
            var occupants = Scrambler.Shuffle(level.Width, level.Height, level.Target, style, new Random(trySeed));
            if (occupants.AsSpan().SequenceEqual(level.Target))
                continue;

            var solution = Solver.Solve(level.Width, level.Height, level.Target, occupants, capacity, nodeBudget);
            if (solution is not null
                && Solver.Replay(level.Width, level.Height, level.Target, occupants, capacity, solution))
                return new Puzzle(level, occupants, capacity, trySeed, solution);
        }

        return null;
    }
}
