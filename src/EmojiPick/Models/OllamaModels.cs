namespace EmojiPick.Models;

/// <summary>
/// Ollama REST API request model (POST /api/generate).
/// </summary>
public class OllamaRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool Stream { get; set; } = false;
    public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    public float Temperature { get; set; } = 0.3f;
    public float TopP { get; set; } = 0.8f;
    public int TopK { get; set; } = 20;
    public int NumPredict { get; set; } = 50;
}

public class OllamaResponse
{
    public string Model { get; set; } = "";
    public string Response { get; set; } = "";
    public bool Done { get; set; }
}
