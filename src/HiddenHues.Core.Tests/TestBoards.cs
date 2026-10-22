using HiddenHues.Core;

namespace HiddenHues.Core.Tests;

internal static class TestBoards
{
    /// <summary>
    /// Builds a board from rows of digits. Each digit is a palette index; '.' in the occupant rows is an empty cell.
    /// </summary>
    public static Board Make(string[] target, string[] occupants)
    {
        var width = target[0].Length;
        var height = target.Length;
        return new Board(width, height, Parse(target), Parse(occupants));

        static int[] Parse(string[] rows) =>
            rows.SelectMany(r => r).Select(ch => ch == '.' ? Board.Empty : ch - '0').ToArray();
    }

    public static void RunUntilIdle(Game game, int maxTicks = 1000)
    {
        for (var i = 0; i < maxTicks && game.Status == GameStatus.Playing && game.InTransit > 0; i++)
            game.Tick();
    }
}
