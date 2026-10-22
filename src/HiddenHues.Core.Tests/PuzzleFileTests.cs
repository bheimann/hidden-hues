using HiddenHues.Core;
using HiddenHues.Core.Generation;

namespace HiddenHues.Core.Tests;

public class PuzzleFileTests
{
    [Fact]
    public void SaveThenLoad_GivesBackTheSamePuzzle()
    {
        var original = Puzzle.Generate(PrototypeLevel.Create(), ShuffleStyle.Clumps, capacity: 30, seed: 4)!;

        var json = PuzzleFile.ToJson(original, "Prototype");
        var (loaded, name, credit, _) = PuzzleFile.FromJson(json);

        Assert.Equal("Prototype", name);
        Assert.Null(credit);
        Assert.Equal(original.Level.Width, loaded.Level.Width);
        Assert.Equal(original.Level.Height, loaded.Level.Height);
        Assert.Equal(original.Level.Palette, loaded.Level.Palette);
        Assert.Equal(original.Level.Target, loaded.Level.Target);
        Assert.Equal(original.Occupants, loaded.Occupants);
        Assert.Equal(original.Capacity, loaded.Capacity);
        Assert.Equal(original.Seed, loaded.Seed);
        Assert.Equal(original.Solution, loaded.Solution);
    }

    [Fact]
    public void Credit_IsKeptWithTheLevel()
    {
        var puzzle = Puzzle.Generate(PrototypeLevel.Create(), ShuffleStyle.Clumps, seed: 4)!;

        var json = PuzzleFile.ToJson(puzzle, "Owl", "Picture by someone on Pixabay", "https://pixabay.com/x/");
        var saved = PuzzleFile.FromJson(json);

        Assert.Equal("Picture by someone on Pixabay", saved.Credit);
        Assert.Equal("https://pixabay.com/x/", saved.CreditUrl);
    }

    [Fact]
    public void Grids_AreSavedAsReadableRows()
    {
        var puzzle = Puzzle.Generate(PrototypeLevel.Create(), ShuffleStyle.Clumps, seed: 4)!;

        var json = PuzzleFile.ToJson(puzzle, "Prototype");

        Assert.Contains("\"target\": [", json);
        Assert.Matches("\"[0-9a-z]{11}\"", json);
    }

    [Fact]
    public void Load_RejectsAScrambleThatDoesNotMatchThePicture()
    {
        const string json = """
            {
              "version": 1, "name": "bad", "width": 2, "height": 1,
              "palette": ["#FF0000", "#0000FF"],
              "target": ["01"], "scrambled": ["00"],
              "capacity": 30, "seed": 1, "solution": []
            }
            """;

        Assert.Throws<FormatException>(() => PuzzleFile.FromJson(json));
    }
}
