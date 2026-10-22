using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiddenHues.Core.Pictures;

/// <summary>The search failed in a way worth telling the player about.</summary>
public sealed class PixabayException : Exception
{
    public PixabayException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// Searches Pixabay for kid-safe illustrations and downloads them. Follows the API's rules:
/// safe search is always on, every search is cached for 24 hours, and pictures are downloaded
/// and kept locally rather than linked to.
/// </summary>
public sealed class PixabayClient
{
    public static readonly TimeSpan SearchCacheTime = TimeSpan.FromHours(24);
    private const string Endpoint = "https://pixabay.com/api/";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _cacheDir;
    private readonly Func<DateTimeOffset> _now;

    public PixabayClient(HttpClient http, string apiKey, string cacheDir, Func<DateTimeOffset>? now = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("A Pixabay API key is required.", nameof(apiKey));

        _http = http;
        _apiKey = apiKey.Trim();
        _cacheDir = cacheDir;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The search URL. Safe search and illustration-only results are always requested.</summary>
    public string SearchUrl(string term, string category, int perPage = 50) =>
        $"{Endpoint}?key={Uri.EscapeDataString(_apiKey)}&q={Uri.EscapeDataString(term)}" +
        $"&category={Uri.EscapeDataString(category)}&image_type=illustration&safesearch=true" +
        $"&per_page={perPage}&order=popular";

    /// <summary>Searches, using the cached result if it's less than 24 hours old.</summary>
    public async Task<IReadOnlyList<PictureHit>> SearchAsync(string term, string category, CancellationToken cancel = default)
    {
        var url = SearchUrl(term, category);
        var cacheFile = Path.Combine(_cacheDir, "searches", Hash(url) + ".json");

        if (File.Exists(cacheFile) && _now() - File.GetLastWriteTimeUtc(cacheFile) < SearchCacheTime)
            return Parse(await File.ReadAllTextAsync(cacheFile, cancel));

        var json = await GetStringAsync(url, cancel);
        var hits = Parse(json);
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        await File.WriteAllTextAsync(cacheFile, json, cancel);
        File.SetLastWriteTimeUtc(cacheFile, _now().UtcDateTime);
        return hits;
    }

    /// <summary>Downloads the picture (about 640 px) once and keeps it, so it's never fetched twice.</summary>
    public async Task<byte[]> DownloadAsync(PictureHit hit, CancellationToken cancel = default)
    {
        var extension = Path.GetExtension(new Uri(hit.WebformatUrl).AbsolutePath);
        var file = Path.Combine(_cacheDir, "pictures", $"{hit.Id}{extension}");
        if (File.Exists(file))
            return await File.ReadAllBytesAsync(file, cancel);

        byte[] bytes;
        try
        {
            bytes = await _http.GetByteArrayAsync(hit.WebformatUrl, cancel);
        }
        catch (HttpRequestException e)
        {
            throw new PixabayException("Couldn't download the picture. Check the internet connection.", e);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllBytesAsync(file, bytes, cancel);
        return bytes;
    }

    internal static IReadOnlyList<PictureHit> Parse(string json)
    {
        var response = JsonSerializer.Deserialize<SearchResponse>(json)
                       ?? throw new PixabayException("Pixabay sent an empty response.");
        return response.Hits
            .Where(h => !string.IsNullOrEmpty(h.WebformatUrl))
            .Select(h => new PictureHit(
                h.Id,
                h.PageUrl,
                h.WebformatUrl,
                h.User,
                h.Tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                h.WebformatWidth,
                h.WebformatHeight))
            .ToList();
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancel)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, cancel);
        }
        catch (HttpRequestException e)
        {
            throw new PixabayException("Couldn't reach Pixabay. Check the internet connection.", e);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancel);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => body,
                HttpStatusCode.TooManyRequests => throw new PixabayException("Too many picture requests. Wait a minute and try again."),
                HttpStatusCode.BadRequest when body.Contains("key", StringComparison.OrdinalIgnoreCase) =>
                    throw new PixabayException("Pixabay didn't accept the API key. Check it under Pixabay key."),
                _ => throw new PixabayException($"Pixabay returned an error ({(int)response.StatusCode})."),
            };
        }
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();

    private sealed class SearchResponse
    {
        [JsonPropertyName("hits")] public List<Hit> Hits { get; set; } = new();
    }

    private sealed class Hit
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("pageURL")] public string PageUrl { get; set; } = "";
        [JsonPropertyName("webformatURL")] public string WebformatUrl { get; set; } = "";
        [JsonPropertyName("webformatWidth")] public int WebformatWidth { get; set; }
        [JsonPropertyName("webformatHeight")] public int WebformatHeight { get; set; }
        [JsonPropertyName("user")] public string User { get; set; } = "";
        [JsonPropertyName("tags")] public string Tags { get; set; } = "";
    }
}
