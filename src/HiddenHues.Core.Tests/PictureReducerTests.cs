using HiddenHues.Core;
using HiddenHues.Core.Generation;

namespace HiddenHues.Core.Tests;

public class PictureReducerTests
{
    private static readonly Rgb Red = new(220, 30, 30);
    private static readonly Rgb Blue = new(30, 60, 220);
    private static readonly Rgb White = new(255, 255, 255);

    [Theory]
    [InlineData(400, 300, 16, 16, 12)]
    [InlineData(300, 400, 16, 12, 16)]
    [InlineData(1000, 100, 16, 16, 4)] // very wide: the short side never drops below the minimum
    public void GridSize_FollowsThePicturesShape(int w, int h, int longSide, int expectedW, int expectedH)
    {
        Assert.Equal((expectedW, expectedH), PictureReducer.GridSize(w, h, longSide));
    }

    [Fact]
    public void Reduce_KeepsLargeAreasOfColorInTheRightPlaces()
    {
        // Left half red, right half blue, at 10x the grid's resolution.
        var image = Paint(80, 80, (x, _) => x < 40 ? Red : Blue);

        var level = PictureReducer.Reduce(image, new ReduceOptions { LongSide = 8, Colors = 4 });

        Assert.Equal(8, level.Width);
        Assert.Equal(8, level.Height);
        Assert.Equal(2, level.Palette.Count); // only two colors exist, even though four were allowed
        var left = level.Target[0];
        var right = level.Target[7];
        Assert.NotEqual(left, right);
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            Assert.Equal(x < 4 ? left : right, level.Target[y * 8 + x]);
    }

    [Fact]
    public void Reduce_MergesColorsThatLookTheSame()
    {
        var nearlyRed = new Rgb(222, 32, 30);
        var image = Paint(40, 40, (x, y) => x < 20 ? Red : y < 20 ? nearlyRed : White);

        var level = PictureReducer.Reduce(image, new ReduceOptions { LongSide = 4, Colors = 3, Despeckle = false });

        Assert.Equal(2, level.Palette.Count);
    }

    [Fact]
    public void Reduce_RespectsTheColorLimit()
    {
        // A smooth gradient has many colors; it must come out with no more than requested.
        var image = Paint(64, 64, (x, y) => new Rgb((byte)(x * 4), (byte)(y * 4), 128));

        var level = PictureReducer.Reduce(image, new ReduceOptions { LongSide = 16, Colors = 5 });

        Assert.InRange(level.Palette.Count, 2, 5);
    }

    [Fact]
    public void Reduce_IsRepeatableForTheSameSeed()
    {
        var image = Paint(64, 48, (x, y) => new Rgb((byte)(x * 4), (byte)(255 - y * 5), (byte)((x + y) * 2)));
        var options = new ReduceOptions { LongSide = 12, Colors = 6, Seed = 42 };

        var a = PictureReducer.Reduce(image, options);
        var b = PictureReducer.Reduce(image, options);

        Assert.Equal(a.Target, b.Target);
        Assert.Equal(a.Palette, b.Palette);
    }

    [Fact]
    public void Despeckle_RemovesALoneCell()
    {
        var grid = new[]
        {
            0, 0, 0,
            0, 1, 0,
            0, 0, 0,
        };

        PictureReducer.Despeckle(grid, 3, 3);

        Assert.All(grid, c => Assert.Equal(0, c));
    }

    [Fact]
    public void Transparency_IsBlendedOntoWhite()
    {
        var image = new PixelImage(1, 1, new byte[] { 0, 0, 0, 0 });

        Assert.Equal(White, image.ColorAt(0, 0));
    }

    [Fact]
    public void Lab_RoundTripsColors()
    {
        foreach (var c in new[] { Red, Blue, White, new Rgb(0, 0, 0), new Rgb(120, 200, 40) })
            Assert.Equal(c, Lab.FromRgb(c).ToRgb());
    }

    private static PixelImage Paint(int width, int height, Func<int, int, Rgb> color)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var c = color(x, y);
            var i = (y * width + x) * 4;
            (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (c.R, c.G, c.B, 255);
        }

        return new PixelImage(width, height, rgba);
    }
}
