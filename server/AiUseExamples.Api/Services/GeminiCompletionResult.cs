namespace AiUseExamples.Api.Services;

public class GeminiCompletionResult
{
    public string Text { get; set; } = string.Empty;
    public List<GeminiFunctionCall> FunctionCalls { get; set; } = new();
    public string? FinishReason { get; set; }
}

