using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EmojiPick.Models;

namespace EmojiPick.Services;

public sealed class OllamaMatcher : ILlmMatcher
{
    private readonly HttpClient _httpClient;
    private readonly ProviderConfig _config;
    private readonly ConcurrentDictionary<string, (List<EmojiEntry> Results, DateTime ExpiresAt)> _cache = new();

    private const string DefaultEndpoint = "http://localhost:11434";
    private const string DefaultModel = "mistral";
    private const int DefaultTimeoutMs = 3000;
    private const int CacheTtlMinutes = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = new SnakeCasePolicy(),
        PropertyNameCaseInsensitive = true,
    };

    public OllamaMatcher(ProviderConfig config) : this(config, handler: null) { }

    public OllamaMatcher(ProviderConfig config, HttpMessageHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;

        var endpoint = string.IsNullOrWhiteSpace(config.Endpoint) ? DefaultEndpoint : config.Endpoint;
        _httpClient = handler is not null
            ? new HttpClient(handler) { BaseAddress = new Uri(endpoint), Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient { BaseAddress = new Uri(endpoint), Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<bool> IsEnabled(CancellationToken cancellationToken = default)
    {
        try
        {
            var timeoutMs = _config.TimeoutMs > 0 ? _config.TimeoutMs : DefaultTimeoutMs;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            var response = await _httpClient.GetAsync("/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<EmojiEntry>> GetLlmRecommendations(
        string selectedText,
        List<EmojiEntry> candidateEmojis,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(selectedText) || candidateEmojis.Count == 0)
            return [];

        var cacheKey = $"{selectedText}|{string.Join(",", candidateEmojis.Select(e => e.Char))}";
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            return cached.Results;

        var timeoutMs = _config.TimeoutMs > 0 ? _config.TimeoutMs : DefaultTimeoutMs;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            var requestBody = JsonSerializer.Serialize(new OllamaRequest
            {
                Model = string.IsNullOrWhiteSpace(_config.Model) ? DefaultModel : _config.Model,
                Prompt = BuildPrompt(selectedText, candidateEmojis),
                Stream = false,
                Options = new OllamaOptions { Temperature = 0.3f, NumPredict = 50 },
            }, JsonOptions);

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync("/api/generate", content, cts.Token);
            httpResponse.EnsureSuccessStatusCode();

            var responseText = await httpResponse.Content.ReadAsStringAsync(cts.Token);
            var ollamaResp = JsonSerializer.Deserialize<OllamaResponse>(responseText, JsonOptions);

            if (ollamaResp is null || string.IsNullOrWhiteSpace(ollamaResp.Response))
                return [];

            var results = ParseEmojiFromResponse(ollamaResp.Response, candidateEmojis);
            _cache[cacheKey] = (results, DateTime.UtcNow.AddMinutes(CacheTtlMinutes));
            return results;
        }
        catch (OperationCanceledException)
        {
            return [];
        }
        catch
        {
            return [];
        }
    }

    private static string BuildPrompt(string text, List<EmojiEntry> candidates)
    {
        var list = string.Join(", ", candidates.Select(e => $"{e.Char} ({e.Name})"));
        return $"""
            Given the following context text: "{text}"

            From these available emoji candidates: {list}

            Reply with only the emoji characters that best match the meaning or emotion of the text, in order of relevance, separated by spaces. No explanations.
            """;
    }

    private static List<EmojiEntry> ParseEmojiFromResponse(string response, List<EmojiEntry> candidates)
    {
        var lookup = candidates.ToDictionary(e => e.Char);
        var result = new List<EmojiEntry>();
        var seen = new HashSet<string>();

        var enumerator = StringInfo.GetTextElementEnumerator(response);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            if (lookup.TryGetValue(element, out var entry) && seen.Add(element))
                result.Add(entry);
        }

        return result;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class SnakeCasePolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(name[i]));
            }
            return sb.ToString();
        }
    }
}
