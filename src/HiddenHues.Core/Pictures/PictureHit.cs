namespace HiddenHues.Core.Pictures;

/// <summary>One picture from a Pixabay search, with what's needed to download it and credit its author.</summary>
public sealed record PictureHit(
    long Id,
    string PageUrl,
    string WebformatUrl,
    string User,
    IReadOnlyList<string> Tags,
    int Width,
    int Height)
{
    /// <summary>The credit line Pixabay asks apps to show wherever their pictures appear.</summary>
    public string Credit => $"Picture by {User} on Pixabay";
}
