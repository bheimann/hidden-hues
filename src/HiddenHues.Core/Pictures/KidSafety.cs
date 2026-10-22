namespace HiddenHues.Core.Pictures;

/// <summary>
/// Kid-friendly search topics, plus a second check on tags. Pixabay's safe search is always on;
/// this is a backstop, not a replacement for a grown-up looking at the preview.
/// </summary>
public static class KidSafety
{
    /// <summary>Search terms, each paired with the Pixabay category it's searched in.</summary>
    public static readonly IReadOnlyList<(string Term, string Category)> Topics = new[]
    {
        ("cat", "animals"), ("dog", "animals"), ("owl", "animals"), ("fox", "animals"), ("fish", "animals"),
        ("turtle", "animals"), ("elephant", "animals"), ("giraffe", "animals"), ("lion", "animals"),
        ("panda", "animals"), ("penguin", "animals"), ("frog", "animals"), ("butterfly", "animals"),
        ("bee", "animals"), ("parrot", "animals"), ("rabbit", "animals"), ("horse", "animals"),
        ("whale", "animals"), ("dinosaur", "animals"), ("ladybug", "animals"),
        ("apple", "food"), ("strawberry", "food"), ("cupcake", "food"), ("watermelon", "food"),
        ("ice cream", "food"), ("banana", "food"), ("pizza", "food"),
        ("flower", "nature"), ("tree", "nature"), ("rainbow", "nature"), ("mushroom", "nature"),
        ("cactus", "nature"), ("sun", "nature"),
        ("car", "transportation"), ("train", "transportation"), ("rocket", "transportation"),
        ("airplane", "transportation"), ("boat", "transportation"), ("bicycle", "transportation"),
    };

    private static readonly HashSet<string> BlockedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "nude", "naked", "nudity", "sexy", "erotic", "bikini", "lingerie", "underwear", "kiss",
        "blood", "bloody", "gore", "gun", "rifle", "pistol", "weapon", "knife", "sword", "war", "soldier",
        "skull", "skeleton", "death", "dead", "corpse", "grave", "horror", "zombie", "monster", "scary",
        "devil", "demon", "hell", "violence", "violent", "fight",
        "smoking", "cigarette", "alcohol", "beer", "wine", "drunk", "drug", "drugs",
    };

    /// <summary>True if none of the picture's tags (or the words in them) are on the blocked list.</summary>
    public static bool TagsLookSafe(IEnumerable<string> tags) =>
        !tags.SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Any(BlockedWords.Contains);
}
