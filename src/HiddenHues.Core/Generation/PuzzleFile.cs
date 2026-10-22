using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiddenHues.Core.Generation;

/// <summary>
/// Saves and loads puzzles as JSON. Grids are stored as rows of base-36 digits (one per cell),
/// so a saved level is readable and small.
/// </summary>
public static class PuzzleFile
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ToJson(Puzzle puzzle, string name)
    {
        var level = puzzle.Level;
        if (level.Palette.Count > 36)
            throw new ArgumentException("Saved levels support at most 36 colors.", nameof(puzzle));

        var dto = new Dto
        {
            Version = CurrentVersion,
            Name = name,
            Width = level.Width,
            Height = level.Height,
            Palette = level.Palette.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList(),
            Target = ToRows(level.Target, level.Width),
            Scrambled = ToRows(puzzle.Occupants, level.Width),
            Capacity = puzzle.Capacity,
            Seed = puzzle.Seed,
            Solution = puzzle.Solution.Select(p => new[] { p.X, p.Y }).ToList(),
        };
        return JsonSerializer.Serialize(dto, Json);
    }

    public static (Puzzle Puzzle, string Name) FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, Json)
                  ?? throw new FormatException("The level file is empty.");
        if (dto.Version != CurrentVersion)
            throw new FormatException($"Level file version {dto.Version} isn't supported (expected {CurrentVersion}).");

        var palette = dto.Palette.Select(Rgb.FromHex).ToList();
        var target = FromRows(dto.Target, dto.Width, dto.Height, palette.Count);
        var occupants = FromRows(dto.Scrambled, dto.Width, dto.Height, palette.Count);
        if (!target.OrderBy(c => c).SequenceEqual(occupants.OrderBy(c => c)))
            throw new FormatException("The scrambled grid doesn't use the same pixels as the target picture.");

        var level = new Level(dto.Width, dto.Height, palette, target);
        var solution = dto.Solution.Select(p => new GridPos(p[0], p[1])).ToList();
        return (new Puzzle(level, occupants, dto.Capacity, dto.Seed, solution), dto.Name);
    }

    private static List<string> ToRows(int[] grid, int width) =>
        grid.Chunk(width).Select(row => new string(row.Select(Digit).ToArray())).ToList();

    private static int[] FromRows(List<string> rows, int width, int height, int colors)
    {
        if (rows.Count != height || rows.Any(r => r.Length != width))
            throw new FormatException($"Expected {height} rows of {width} cells.");

        return rows.SelectMany(r => r).Select(ch =>
        {
            var value = ch is >= '0' and <= '9' ? ch - '0' : char.ToLowerInvariant(ch) - 'a' + 10;
            if (value < 0 || value >= colors)
                throw new FormatException($"Cell '{ch}' isn't a palette color.");
            return value;
        }).ToArray();
    }

    private static char Digit(int value) => value < 10 ? (char)('0' + value) : (char)('a' + value - 10);

    private sealed class Dto
    {
        public int Version { get; set; }
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> Palette { get; set; } = new();
        public List<string> Target { get; set; } = new();
        public List<string> Scrambled { get; set; } = new();
        public int Capacity { get; set; } = Game.DefaultCapacity;
        public int Seed { get; set; }

        [JsonPropertyName("solution")]
        public List<int[]> Solution { get; set; } = new();
    }
}
