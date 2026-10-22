namespace HiddenHues.Core.Generation;

/// <summary>
/// Finds a sequence of clicks that wins a board without jamming the belt.
/// <para>
/// It plays a "settled" model: after each pick it lets every pixel that can drop do so before picking again.
/// A real player can always wait like that, so anything solvable this way is solvable in the game.
/// Inside the search, pixels fill matching slots in reading order rather than by belt position, which is
/// much faster than ticking. <see cref="Replay"/> then checks the solution against the real simulation.
/// </para>
/// </summary>
public static class Solver
{
    private const int MaxSettleTicks = 100_000;

    public static IReadOnlyList<GridPos>? Solve(int width, int height, int[] target, int[] occupants, int capacity,
        int nodeBudget = 20_000)
    {
        var search = new Search(width, height, target, capacity, nodeBudget);
        var start = new State((int[])occupants.Clone(), new int[target.Max() + 1], 0);
        var path = new List<GridPos>();
        return search.Run(start, path) ? path : null;
    }

    /// <summary>Plays the clicks in the real simulation, waiting for the belt to settle after each one.</summary>
    public static bool Replay(int width, int height, int[] target, int[] occupants, int capacity,
        IReadOnlyList<GridPos> clicks)
    {
        var game = new Game(new Board(width, height, target, occupants), capacity);
        foreach (var click in clicks)
        {
            if (game.Pick(click).Count == 0)
                return false;
            Settle(game);
            if (game.Status != GameStatus.Playing)
                break;
        }

        return game.Status == GameStatus.Won;
    }

    /// <summary>Ticks until the feeder is empty and nothing on the belt can drop.</summary>
    public static void Settle(Game game)
    {
        for (var i = 0; i < MaxSettleTicks && game.Status == GameStatus.Playing && CanStillDrop(game); i++)
            game.Tick();

        static bool CanStillDrop(Game g) =>
            g.FeederCount > 0 || g.Belt.Any(p => p is { } pixel && g.Board.HasOpenSlotFor(pixel.Color));
    }

    private sealed record State(int[] Occupants, int[] Circulating, int CirculatingTotal);

    private sealed class Search
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int[] _target;
        private readonly int _capacity;
        private readonly int _budget;
        private readonly HashSet<ulong> _seen = new();
        private int _nodes;

        public Search(int width, int height, int[] target, int capacity, int budget)
        {
            _width = width;
            _height = height;
            _target = target;
            _capacity = capacity;
            _budget = budget;
        }

        public bool Run(State state, List<GridPos> path)
        {
            if (IsSolved(state))
                return true;
            if (++_nodes > _budget || !_seen.Add(Hash(state)))
                return false;

            var moves = new List<(GridPos Click, State Next, int Taken)>();
            foreach (var region in Regions(state))
            {
                // Picking pixels that already sit on matching slots only sends them around and back.
                if (region.All(i => state.Occupants[i] == _target[i]))
                    continue;

                var click = region[0];
                var (next, taken) = Apply(state, click);
                if (next.CirculatingTotal >= _capacity && !IsSolved(next))
                    continue; // jammed

                moves.Add((new GridPos(click % _width, click / _width), next, taken));
            }

            // Keep the belt as empty as possible; among equals, prefer picks that move more pixels.
            foreach (var move in moves.OrderBy(m => m.Next.CirculatingTotal).ThenByDescending(m => m.Taken))
            {
                path.Add(move.Click);
                if (Run(move.Next, path))
                    return true;
                path.RemoveAt(path.Count - 1);
                if (_nodes > _budget)
                    return false;
            }

            return false;
        }

        private (State Next, int Taken) Apply(State state, int click)
        {
            var occupants = (int[])state.Occupants.Clone();
            var circulating = (int[])state.Circulating.Clone();
            var total = state.CirculatingTotal;
            var color = occupants[click];
            var room = _capacity - total;

            // Same breadth-first, capped pick as Game.Pick.
            var queue = new Queue<int>();
            var seen = new HashSet<int> { click };
            queue.Enqueue(click);
            var taken = 0;
            while (queue.Count > 0 && taken < room)
            {
                var c = queue.Dequeue();
                occupants[c] = Board.Empty;
                taken++;
                foreach (var n in Neighbors(c))
                    if (state.Occupants[n] == color && seen.Add(n))
                        queue.Enqueue(n);
            }

            circulating[color] += taken;
            total += taken;

            // Settle: every circulating pixel with an open matching slot drops in.
            for (var i = 0; i < occupants.Length && total > 0; i++)
            {
                if (occupants[i] != Board.Empty || circulating[_target[i]] == 0)
                    continue;
                occupants[i] = _target[i];
                circulating[_target[i]]--;
                total--;
            }

            return (new State(occupants, circulating, total), taken);
        }

        /// <summary>Connected same-colored groups of pixels, each listed from its first cell in reading order.</summary>
        private List<List<int>> Regions(State state)
        {
            var regions = new List<List<int>>();
            var visited = new bool[state.Occupants.Length];
            for (var start = 0; start < state.Occupants.Length; start++)
            {
                if (visited[start] || state.Occupants[start] == Board.Empty)
                    continue;

                var color = state.Occupants[start];
                var region = new List<int>();
                var stack = new Stack<int>();
                stack.Push(start);
                visited[start] = true;
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    region.Add(c);
                    foreach (var n in Neighbors(c))
                        if (!visited[n] && state.Occupants[n] == color)
                        {
                            visited[n] = true;
                            stack.Push(n);
                        }
                }

                region.Sort();
                regions.Add(region);
            }

            return regions;
        }

        private IEnumerable<int> Neighbors(int cell)
        {
            var (x, y) = (cell % _width, cell / _width);
            if (y > 0) yield return cell - _width;
            if (x < _width - 1) yield return cell + 1;
            if (y < _height - 1) yield return cell + _width;
            if (x > 0) yield return cell - 1;
        }

        private bool IsSolved(State state) => state.CirculatingTotal == 0 && state.Occupants.AsSpan().SequenceEqual(_target);

        private static ulong Hash(State state)
        {
            // FNV-1a over the board and the circulating counts. A rare collision only prunes one branch.
            var hash = 14695981039346656037UL;
            foreach (var v in state.Occupants) hash = (hash ^ (ulong)(v + 1)) * 1099511628211UL;
            foreach (var v in state.Circulating) hash = (hash ^ (ulong)v) * 1099511628211UL;
            return hash;
        }
    }
}
