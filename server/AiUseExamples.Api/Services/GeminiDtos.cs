using System.Text.Json.Serialization;

namespace AiUseExamples.Api.Services;

// DTOs representing the Gemini REST API response structure.
public class GeminiPart
{
    public string? Text { get; set; }
    
    [JsonPropertyName("functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }
    
    [JsonPropertyName("functionResponse")]
    public GeminiFunctionResponse? FunctionResponse { get; set; }
}

public class GeminiFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("args")]
    public Dictionary<string, object> Args { get; set; } = new();
}

public class GeminiFunctionResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("response")]
    public object Response { get; set; } = new();
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
    
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
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

