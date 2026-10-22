using HiddenHues.Core;

namespace HiddenHues.Core.Tests;

public class GameTests
{
    [Fact]
    public void Feeder_ReleasesOnePixelPerTick()
    {
        var board = TestBoards.Make(new[] { "000", "000", "000" }, new[] { "111", "000", "000" });
        var game = new Game(board);

        game.Pick(new GridPos(0, 0));
        game.Tick();

        Assert.Equal(2, game.FeederCount);
        Assert.Equal(1, game.Belt.Count(p => p is not null));
    }

    [Fact]
    public void Pixel_FillsTheNearerOfTwoMatchingCellsFirst()
    {
        // 5x5, feeder above column 2. Cells (2,0) and (2,1) both want a 1 and both belong to the top tile.
        var board = TestBoards.Make(
            new[] { "00100", "00100", "00000", "00000", "22000" },
            new[] { "00200", "00200", "00000", "00000", "11000" });
        var game = new Game(board, feederColumn: 2);

        game.Pick(new GridPos(2, 0)); // the two 2s, opening both 1-slots
        game.Pick(new GridPos(0, 4)); // the two 1s, queued behind the 2s

        game.Tick();
        game.Tick();
        var third = game.Tick();  // first 1 released onto the top of column 2
        var fourth = game.Tick(); // second 1 released

        Assert.Equal(new GridPos(2, 0), Assert.Single(third).Cell);
        Assert.Equal(new GridPos(2, 1), Assert.Single(fourth).Cell);
    }

    [Fact]
    public void EqualDistanceCell_IsFilledFromWhicheverHomeTileComesFirst()
    {
        // 3x3, feeder above the middle column. Cell (2,0) is equally near the top and the right.
        // The top tile (2,-1) comes before the right one (3,0), so the 1 drops in from the top.
        var board = TestBoards.Make(
            new[] { "001", "000", "000" },
            new[] { "100", "000", "000" });
        var game = new Game(board, feederColumn: 1);

        game.Pick(new GridPos(0, 0)); // the lone 1, whose own slot wants a 0
        game.Pick(new GridPos(2, 0)); // the connected 0s, which opens the 1-slot at (2,0)

        game.Tick(); // 1 released at (1,-1)
        var placements = game.Tick(); // 1 moves to (2,-1)

        var placement = Assert.Single(placements, p => p.Pixel.Color == 1);
        Assert.Equal(new GridPos(2, 0), placement.Cell);
        Assert.Equal(new GridPos(2, -1), game.Layout.Tiles[placement.Tile]);
    }

    [Fact]
    public void Game_IsWon_WhenEveryCellHoldsItsTargetColor()
    {
        var board = TestBoards.Make(new[] { "01", "10" }, new[] { "10", "01" });
        var game = new Game(board);

        game.Pick(new GridPos(0, 0));
        game.Pick(new GridPos(1, 0));
        game.Pick(new GridPos(0, 1));
        game.Pick(new GridPos(1, 1));
        TestBoards.RunUntilIdle(game);

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(0, game.InTransit);
    }

    [Fact]
    public void Game_IsLost_WhenFullAndNothingInTransitHasASlot()
    {
        // Color 1 fills the first row but its slots are in the second row, which is still occupied.
        var board = TestBoards.Make(
            new[] { "000", "111" },
            new[] { "111", "000" });
        var game = new Game(board, capacity: 3);

        game.Pick(new GridPos(0, 0));

        Assert.Equal(GameStatus.Lost, game.Status);
    }

    [Fact]
    public void Game_IsNotLost_WhenFullButSomethingCanStillDrop()
    {
        var board = TestBoards.Make(
            new[] { "100", "011" },
            new[] { "111", "000" });
        var game = new Game(board, capacity: 3);

        game.Pick(new GridPos(0, 0));

        Assert.Equal(GameStatus.Playing, game.Status);
    }

    [Fact]
    public void PrototypeLevel_ScramblesIntoAPlayableBoard()
    {
        var level = PrototypeLevel.Create();
        var board = level.CreateScrambledBoard(seed: 1);

        Assert.Equal(11, board.Width);
        Assert.Equal(14, board.Height);
        Assert.False(board.IsSolved);
    }
}
