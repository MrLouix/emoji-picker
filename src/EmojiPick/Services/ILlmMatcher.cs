using EmojiPick.Models;

namespace EmojiPick.Services;

/// <summary>
/// Unified interface for LLM-based emoji recommendation.
/// Implemented by OllamaMatcher and LlamaSharpMatcher.
/// </summary>
public interface ILlmMatcher : IDisposable
{
    Task<List<EmojiEntry>> GetLlmRecommendations(
        string selectedText,
        List<EmojiEntry> candidateEmojis,
        CancellationToken cancellationToken = default);
}
