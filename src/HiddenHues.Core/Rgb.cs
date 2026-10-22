namespace HiddenHues.Core;

/// <summary>An engine-independent color. The game converts it to a Godot color for drawing.</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            throw new FormatException($"Expected a 6-digit hex color, got '{hex}'.");
        return new Rgb(
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..], 16));
    }
}
