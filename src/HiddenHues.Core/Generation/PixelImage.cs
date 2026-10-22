namespace HiddenHues.Core.Generation;

/// <summary>A decoded picture as raw RGBA bytes, row by row. The game fills this from a Godot image.</summary>
public sealed class PixelImage
{
    public PixelImage(int width, int height, byte[] rgba)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image must be at least 1x1.");
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("Expected 4 bytes (RGBA) per pixel.", nameof(rgba));

        Width = width;
        Height = height;
        Rgba = rgba;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Rgba { get; }

    /// <summary>The pixel's color, with any transparency blended onto white.</summary>
    public Rgb ColorAt(int x, int y)
    {
        var i = (y * Width + x) * 4;
        var a = Rgba[i + 3] / 255.0;
        return new Rgb(Blend(Rgba[i]), Blend(Rgba[i + 1]), Blend(Rgba[i + 2]));

        byte Blend(byte channel) => (byte)Math.Round(channel * a + 255 * (1 - a));
    }
}
