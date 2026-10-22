using HiddenHues.Core;

namespace HiddenHues.Core.Tests;

public class PickTests
{
    [Fact]
    public void Pick_TakesConnectedSameColorPixels_NotDiagonals()
    {
        var board = TestBoards.Make(
            new[] { "000", "000", "000" },
            new[] { "110", "102", "001" });
        var game = new Game(board);

        var taken = game.Pick(new GridPos(0, 0));

        Assert.Equal(
            new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1) }.ToHashSet(),
            taken.ToHashSet());
        Assert.Equal(3, game.FeederCount);
        Assert.Equal(1, board.OccupantAt(new GridPos(2, 2))); // a 1, but not connected to the region
    }

    [Fact]
    public void Pick_StopsAtCapacity_TakingNearestCellsFirst()
    {
        var board = TestBoards.Make(
            new[] { "00000" },
            new[] { "11111" });
        var game = new Game(board, capacity: 3);

        var taken = game.Pick(new GridPos(2, 0));

        Assert.Equal(3, taken.Count);
        Assert.Equal(new GridPos(2, 0), taken[0]);
        Assert.Contains(new GridPos(1, 0), taken);
        Assert.Contains(new GridPos(3, 0), taken);
        Assert.False(board.IsEmpty(new GridPos(0, 0)));
        Assert.False(board.IsEmpty(new GridPos(4, 0)));
    }

    [Fact]
    public void Pick_OnEmptyCell_TakesNothing()
    {
        var board = TestBoards.Make(new[] { "00" }, new[] { ".0" });
        var game = new Game(board);

        Assert.Empty(game.Pick(new GridPos(0, 0)));
        Assert.Equal(0, game.InTransit);
    }

    [Fact]
    public void Pick_TakesPixelsAlreadyInMatchingSlots()
    {
        var board = TestBoards.Make(new[] { "012" }, new[] { "021" }); // (0,0) already matches
        var game = new Game(board);

        var taken = game.Pick(new GridPos(0, 0));

        Assert.Single(taken);
        Assert.True(board.IsEmpty(new GridPos(0, 0)));
    }

    [Fact]
    public void Pick_WhileBeltIsMoving_OnlyTakesWhatFits()
    {
        var board = TestBoards.Make(
            new[] { "0000", "0000", "0000", "0000" },
            new[] { "1111", "2222", "0000", "0000" });
        var game = new Game(board, capacity: 6);

        game.Pick(new GridPos(0, 0)); // four 1s
        game.Tick();
        game.Tick();

        var taken = game.Pick(new GridPos(0, 1)); // four 2s, but only two fit

        Assert.Equal(2, taken.Count);
        Assert.Equal(6, game.InTransit);
    }
}
