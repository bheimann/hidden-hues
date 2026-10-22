namespace HiddenHues.Core.Pictures;

/// <summary>
/// Picks a random kid-safe picture: a random topic, a search (cached), then a random hit that passes the
/// tag check and hasn't been hidden or shown recently.
/// </summary>
public sealed class RandomPictures
{
    private const int RecentLimit = 50;
    private const int TopicsToTry = 5;

    private readonly PixabayClient _client;
    private readonly Random _rng;
    private readonly HashSet<long> _hidden;
    private readonly Queue<long> _recent = new();

    public RandomPictures(PixabayClient client, IEnumerable<long> hiddenIds, Random? rng = null)
    {
        _client = client;
        _hidden = new HashSet<long>(hiddenIds);
        _rng = rng ?? new Random();
    }

    /// <summary>Never offer this picture again (for example, a grown-up rejected it).</summary>
    public void Hide(long id) => _hidden.Add(id);

    public IReadOnlyCollection<long> HiddenIds => _hidden;

    /// <summary>Returns a picture and its downloaded bytes, or null if nothing suitable turned up.</summary>
    public async Task<(PictureHit Hit, byte[] Bytes)?> NextAsync(CancellationToken cancel = default)
    {
        for (var attempt = 0; attempt < TopicsToTry; attempt++)
        {
            var (term, category) = KidSafety.Topics[_rng.Next(KidSafety.Topics.Count)];
            var candidates = (await _client.SearchAsync(term, category, cancel))
                .Where(h => !_hidden.Contains(h.Id) && !_recent.Contains(h.Id) && KidSafety.TagsLookSafe(h.Tags))
                .ToList();
            if (candidates.Count == 0)
                continue;

            var hit = candidates[_rng.Next(candidates.Count)];
            _recent.Enqueue(hit.Id);
            if (_recent.Count > RecentLimit)
                _recent.Dequeue();

            return (hit, await _client.DownloadAsync(hit, cancel));
        }

        return null;
    }
}
