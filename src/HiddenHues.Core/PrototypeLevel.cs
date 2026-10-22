namespace HiddenHues.Core;

/// <summary>The hard-coded phase 1 level: the 11x14 character from the Brick Factory reference screenshot.</summary>
public static class PrototypeLevel
{
    private static readonly string[] Rows =
    {
        "GGMBBMBBMGG",
        "GMMBMMBMMMG",
        "GMSWWSWWSMG",
        "GMSWKSWKSMG",
        "GSSSSSSSSGG",
        "GGSRSSSRSGG",
        "GGGSRRRSGGG",
        "GSGGSSSGGSG",
        "GSSSUUUSSSG",
        "GGGGUUUGGGG",
        "GGRGUUUGGGG",
        "GGRKKKKGGGG",
        "GGGGGGKGGGG",
        "DDDDDDRRDDD",
    };

    private static readonly Dictionary<char, Rgb> Colors = new()
    {
        ['G'] = Rgb.FromHex("2ECC71"), // green
        ['D'] = Rgb.FromHex("1E7A4C"), // dark green
        ['M'] = Rgb.FromHex("7B2D3B"), // maroon
        ['B'] = Rgb.FromHex("9A5A1E"), // brown
        ['S'] = Rgb.FromHex("F5C6A5"), // skin
        ['W'] = Rgb.FromHex("F4F4F4"), // white
        ['K'] = Rgb.FromHex("2B2B2B"), // black
        ['R'] = Rgb.FromHex("E53935"), // red
        ['U'] = Rgb.FromHex("1E6FE0"), // blue
    };

    public static Level Create() => Level.FromRows(Rows, Colors);
}
