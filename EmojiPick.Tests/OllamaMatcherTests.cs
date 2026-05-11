using System.Net;
using System.Net.Http;
using EmojiPick.Models;
using EmojiPick.Services;
using Xunit;

namespace EmojiPick.Tests;

public class OllamaMatcherTests
{
    private static ProviderConfig DefaultConfig() => new()
    {
        Enabled = true,
        Endpoint = "http://localhost:11434",
        Model = "mistral",
        TimeoutMs = 3000,
    };

    private static List<EmojiEntry> SampleCandidates() =>
        new List<EmojiEntry>
        {
            new() { Char = "😀", Name = "grinning face", Tags = new List<string> { "happy", "smile" }, Category = "smileys" },
            new() { Char = "❤️", Name = "red heart",     Tags = new List<string> { "love", "heart" },  Category = "symbols" },
            new() { Char = "🔥", Name = "fire",           Tags = new List<string> { "hot", "fire" },    Category = "objects" },
        };

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new OllamaMatcher(null!));
    }

    // ── IsEnabled ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsEnabled_ReturnsTrueWhenOllamaRespondsWithTags()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"models":[]}"""),
            }));
        using var matcher = new OllamaMatcher(DefaultConfig(), handler);

        Assert.True(await matcher.IsEnabled());
    }

    [Fact]
    public async Task IsEnabled_ReturnsFalseWhenEndpointUnavailable()
    {
        var handler = new FakeHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        using var matcher = new OllamaMatcher(DefaultConfig(), handler);

        Assert.False(await matcher.IsEnabled());
    }

    // ── GetLlmRecommendations ─────────────────────────────────────────────────

    [Fact]
    public async Task GetLlmRecommendations_EmptyText_ReturnsEmpty()
    {
        using var matcher = new OllamaMatcher(DefaultConfig());

        var result = await matcher.GetLlmRecommendations("", SampleCandidates());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLlmRecommendations_WhitespaceText_ReturnsEmpty()
    {
        using var matcher = new OllamaMatcher(DefaultConfig());

        var result = await matcher.GetLlmRecommendations("   ", SampleCandidates());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLlmRecommendations_ParsesEmojiFromLlmResponse()
    {
        const string responseJson =
            """{"model":"mistral","response":"Best matches: 😀 and 🔥 here.","done":true}""";
        var handler = new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson),
            }));
        using var matcher = new OllamaMatcher(DefaultConfig(), handler);

        var result = await matcher.GetLlmRecommendations("happy fire", SampleCandidates());

        Assert.Equal(2, result.Count);
        Assert.Equal("😀", result[0].Char);
        Assert.Equal("🔥", result[1].Char);
    }

    [Fact]
    public async Task GetLlmRecommendations_CancelledToken_ReturnsEmpty()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var matcher = new OllamaMatcher(DefaultConfig(), handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await matcher.GetLlmRecommendations("happy", SampleCandidates(), cts.Token);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLlmRecommendations_CachesResultsForSameQuery()
    {
        int callCount = 0;
        const string responseJson =
            """{"model":"mistral","response":"😀","done":true}""";
        var handler = new FakeHandler((_, _) =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson),
            });
        });
        using var matcher = new OllamaMatcher(DefaultConfig(), handler);
        var candidates = SampleCandidates();

        await matcher.GetLlmRecommendations("happy", candidates);
        await matcher.GetLlmRecommendations("happy", candidates);

        Assert.Equal(1, callCount);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var matcher = new OllamaMatcher(DefaultConfig());
        var ex = Record.Exception(matcher.Dispose);
        Assert.Null(ex);
    }

    // ── Fake HTTP handler ─────────────────────────────────────────────────────

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            => _send = send;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _send(request, cancellationToken);
    }
}
