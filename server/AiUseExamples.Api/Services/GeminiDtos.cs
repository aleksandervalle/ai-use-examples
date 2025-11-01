using System.Text.Json.Serialization;

namespace AiUseExamples.Api.Services;

// DTOs representing the Gemini REST API response structure.
public class GeminiPart
{
    public string? Text { get; set; }
}

public class GeminiContent
{
    public List<GeminiPart>? Parts { get; set; }
    public string? Role { get; set; }
}

public class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiResponse
{
    public List<GeminiCandidate>? Candidates { get; set; }
    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public long? PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public long? CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public long? TotalTokenCount { get; set; }
}

// DTO to capture the structure of a single chunk from the Gemini stream
public class GeminiStreamChunk
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

