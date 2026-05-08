using EmojiPick.Models;

namespace EmojiPick.Services;

/// <summary>
/// Ollama LLM integration — HTTP POST to localhost:11434/api/generate.
/// Phase 6 placeholder — implemented later.
/// </summary>
public class OllamaMatcher : ILlmMatcher
{
    // TODO: implement HTTP client POST to Ollama endpoint
    public Task<List<EmojiEntry>> GetLlmRecommendations(
        string selectedText,
        List<EmojiEntry> candidateEmojis,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<EmojiEntry>());
    }

    public void Dispose() { }
}
