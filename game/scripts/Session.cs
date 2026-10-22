using HiddenHues.Core.Generation;

namespace HiddenHues;

/// <summary>What the generator hands to the play screen.</summary>
public static class Session
{
    public static Puzzle? Puzzle { get; set; }
    public static string Name { get; set; } = "Sample";
    public static ShuffleStyle Style { get; set; } = ShuffleStyle.Clumps;
}
