using System.Net;
using HiddenHues.Core.Pictures;

namespace HiddenHues.Core.Tests;

public sealed class PixabayTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "hidden-hues-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeHandler _handler = new();
    private DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public void SearchUrl_AlwaysAsksForSafeIllustrations()
    {
        var url = Client().SearchUrl("ice cream", "food");

        Assert.Contains("safesearch=true", url);
        Assert.Contains("image_type=illustration", url);
        Assert.Contains("q=ice%20cream", url);
        Assert.Contains("category=food", url);
        Assert.Contains("key=test-key", url);
    }

    [Fact]
    public async Task Search_ParsesHits()
    {
        _handler.Json = SearchJson((1, "owl, bird, cute"), (2, "owl, night"));

        var hits = await Client().SearchAsync("owl", "animals");

        Assert.Equal(2, hits.Count);
        Assert.Equal(1, hits[0].Id);
        Assert.Equal(new[] { "owl", "bird", "cute" }, hits[0].Tags);
        Assert.Equal("Picture by artist1 on Pixabay", hits[0].Credit);
        Assert.EndsWith("_640.png", hits[0].WebformatUrl);
    }

    [Fact]
    public async Task Search_IsCachedFor24Hours()
    {
        _handler.Json = SearchJson((1, "owl"));
        var client = Client();

        await client.SearchAsync("owl", "animals");
        _now += TimeSpan.FromHours(23);
        await client.SearchAsync("owl", "animals");
        Assert.Equal(1, _handler.SearchRequests);

        _now += TimeSpan.FromHours(2);
        await client.SearchAsync("owl", "animals");
        Assert.Equal(2, _handler.SearchRequests);
    }

    [Fact]
    public async Task Download_FetchesEachPictureOnce()
    {
        _handler.Json = SearchJson((7, "cat"));
        var client = Client();
        var hit = (await client.SearchAsync("cat", "animals"))[0];

        var first = await client.DownloadAsync(hit);
        var second = await client.DownloadAsync(hit);

        Assert.Equal(first, second);
        Assert.Equal(1, _handler.DownloadRequests);
    }

    [Fact]
    public async Task BadKey_GivesAFriendlyMessage()
    {
        _handler.Status = HttpStatusCode.BadRequest;
        _handler.Json = "[ERROR 400] Invalid or missing API key ([Pixabay API key])";

        var error = await Assert.ThrowsAsync<PixabayException>(() => Client().SearchAsync("cat", "animals"));

        Assert.Contains("API key", error.Message);
    }

    [Fact]
    public async Task RateLimit_GivesAFriendlyMessage()
    {
        _handler.Status = HttpStatusCode.TooManyRequests;
        _handler.Json = "API rate limit exceeded";

        var error = await Assert.ThrowsAsync<PixabayException>(() => Client().SearchAsync("cat", "animals"));

        Assert.Contains("Wait a minute", error.Message);
    }

    [Theory]
    [InlineData("cute, owl, bird", true)]
    [InlineData("pirate, skull, flag", false)]
    [InlineData("glass of wine", false)] // multi-word tags are checked word by word
    public void TagCheck_BlocksUnsuitableWords(string tags, bool safe)
    {
        Assert.Equal(safe, KidSafety.TagsLookSafe(tags.Split(", ")));
    }

    [Fact]
    public async Task RandomPictures_SkipsHiddenAndUnsafePictures()
    {
        _handler.Json = SearchJson((1, "owl"), (2, "skull"), (3, "cat"));
        var pictures = new RandomPictures(Client(), hiddenIds: new long[] { 1 }, new Random(1));

        var picked = await pictures.NextAsync();

        Assert.NotNull(picked);
        Assert.Equal(3, picked!.Value.Hit.Id);
    }

    [Fact]
    public async Task RandomPictures_DoesNotRepeatRecentPictures()
    {
        _handler.Json = SearchJson((1, "owl"), (2, "cat"));
        var pictures = new RandomPictures(Client(), Array.Empty<long>(), new Random(1));

        var first = await pictures.NextAsync();
        var second = await pictures.NextAsync();
        var third = await pictures.NextAsync();

        Assert.NotEqual(first!.Value.Hit.Id, second!.Value.Hit.Id);
        Assert.Null(third); // both have been shown recently
    }

    private PixabayClient Client() => new(new HttpClient(_handler), "test-key", _cacheDir, () => _now);

    private static string SearchJson(params (long Id, string Tags)[] hits)
    {
        var items = hits.Select(h => $$"""
            {
              "id": {{h.Id}},
              "pageURL": "https://pixabay.com/illustrations/x-{{h.Id}}/",
              "webformatURL": "https://pixabay.com/get/pic{{h.Id}}_640.png",
              "webformatWidth": 640, "webformatHeight": 480,
              "user": "artist{{h.Id}}", "tags": "{{h.Tags}}"
            }
            """);
        return $$"""{ "total": {{hits.Length}}, "totalHits": {{hits.Length}}, "hits": [{{string.Join(",", items)}}] }""";
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string Json { get; set; } = """{ "hits": [] }""";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public int SearchRequests { get; private set; }
        public int DownloadRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
        {
            if (request.RequestUri!.AbsolutePath.StartsWith("/get/"))
            {
                DownloadRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3 }),
                });
            }

            SearchRequests++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Json) });
        }
    }
}
