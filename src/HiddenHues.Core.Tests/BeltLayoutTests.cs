using HiddenHues.Core;

namespace HiddenHues.Core.Tests;

public class BeltLayoutTests
{
    [Fact]
    public void Loop_RingsTheGrid_StartingAtTheFeeder()
    {
        var layout = new BeltLayout(width: 11, height: 14, feederColumn: 4);

        Assert.Equal(2 * 11 + 2 * 14 + 4, layout.Length);
        Assert.Equal(new GridPos(4, -1), layout.Tiles[0]);
        Assert.Equal(new GridPos(5, -1), layout.Tiles[1]); // clockwise: along the top to the right
        Assert.Equal(layout.Length, layout.Tiles.Distinct().Count());
    }

    [Fact]
    public void Cell_ClosestToOneSide_HasOneHomeTileAcrossFromIt()
    {
        var layout = new BeltLayout(width: 5, height: 9, feederColumn: 2);

        var homes = layout.HomeTilesOf(new GridPos(2, 1));

        Assert.Single(homes);
        Assert.Equal(new GridPos(2, -1), layout.Tiles[homes[0]]);
    }

    [Fact]
    public void Cell_EquallyCloseToTwoSides_HasAHomeTileOnEach()
    {
        var layout = new BeltLayout(width: 5, height: 9, feederColumn: 2);

        var homes = layout.HomeTilesOf(new GridPos(0, 0)).Select(t => layout.Tiles[t]).ToHashSet();

        Assert.Equal(new[] { new GridPos(0, -1), new GridPos(-1, 0) }.ToHashSet(), homes);
    }

    [Fact]
    public void Tile_ListsTheCellsItServes_ClosestFirst()
    {
        var layout = new BeltLayout(width: 5, height: 9, feederColumn: 2);
        var topOfColumn2 = layout.Tiles.ToList().IndexOf(new GridPos(2, -1));

        // Column 2 of a 5-wide board: rows 0-2 are nearest the top (rows 3+ are nearer a side, so they aren't here).
        Assert.Equal(
            new[] { new GridPos(2, 0), new GridPos(2, 1), new GridPos(2, 2) },
            layout.HomesAt(topOfColumn2));
    }

    [Fact]
    public void CornerTiles_ServeNoCells()
    {
        var layout = new BeltLayout(width: 3, height: 3, feederColumn: 1);
        var corner = layout.Tiles.ToList().IndexOf(new GridPos(-1, -1));

        Assert.Empty(layout.HomesAt(corner));
    }
}
