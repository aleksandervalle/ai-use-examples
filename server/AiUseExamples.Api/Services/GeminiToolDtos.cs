using System.Text.Json.Serialization;

namespace AiUseExamples.Api.Services;

// DTOs for Gemini Function Calling (Tools)
public class GeminiFunctionParameterSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public Dictionary<string, GeminiFunctionParameterProperty>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

public class GeminiFunctionParameterProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("items")]
    public GeminiFunctionParameterProperty? Items { get; set; }
}

public class GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public GeminiFunctionParameterSchema Parameters { get; set; } = new();
}

public class GeminiTool
{
    [JsonPropertyName("functionDeclarations")]
    public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new();
}

