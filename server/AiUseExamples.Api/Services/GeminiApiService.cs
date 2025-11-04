using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Services;

public enum GeminiEmbeddingTaskType
{
    TaskTypeUnspecified,
    RetrievalDocument,
    RetrievalQuery
}

public interface IGeminiApiService
{
    Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken, List<GeminiTool>? tools = null);
    Task<GeminiCompletionResult> GenerateCompletionWithToolsAsync(List<object> contents, List<GeminiTool>? tools, CancellationToken cancellationToken);
    Task<string> GenerateMultimodalCompletionAsync(string prompt, byte[] imageData, string mimeType, CancellationToken cancellationToken);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);
    Task<float[]> GenerateEmbeddingAsync(string text, GeminiEmbeddingTaskType taskType, CancellationToken cancellationToken);
}

public class GeminiApiService : IGeminiApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly ILogger<GeminiApiService> _logger;
    private readonly string _apiKey;
    private readonly string _geminiEndpoint;
    private readonly string _embeddingModel;
    private readonly int _embeddingDimensions;

    public GeminiApiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
        
        _geminiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent";
        _embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "gemini-embedding-001";
        _embeddingDimensions = int.TryParse(configuration["Gemini:EmbeddingDimensions"], out var dims) ? dims : 1536;
        
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        => await GenerateEmbeddingAsync(text, GeminiEmbeddingTaskType.RetrievalDocument, cancellationToken);

    public async Task<float[]> GenerateEmbeddingAsync(string text, GeminiEmbeddingTaskType taskType, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Requesting embedding from Gemini model {Model} with taskType {TaskType}", _embeddingModel, taskType);

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_embeddingModel}:embedContent";

            var requestBody = new Dictionary<string, object>
            {
                ["model"] = $"models/{_embeddingModel}",
                ["content"] = new Dictionary<string, object>
                {
                    ["parts"] = new object[] { new Dictionary<string, object> { ["text"] = text } }
                },
                ["taskType"] = taskType switch
                {
                    GeminiEmbeddingTaskType.RetrievalDocument => "RETRIEVAL_DOCUMENT",
                    GeminiEmbeddingTaskType.RetrievalQuery => "RETRIEVAL_QUERY",
                    _ => "RETRIEVAL_DOCUMENT"
                },
                // Match production-proven request shape
                ["output_dimensionality"] = _embeddingDimensions
            };

            var jsonBody = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", _apiKey);

            var client = _httpClientFactory.CreateClient(nameof(GeminiApiService));
            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini Embedding API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Gemini Embedding API error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var geminiResponse = JsonSerializer.Deserialize<GeminiEmbeddingResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (geminiResponse?.Embedding?.Values != null && geminiResponse.Embedding.Values.Any())
            {
                return NormalizeVector(geminiResponse.Embedding.Values.ToArray());
            }

            _logger.LogError("Gemini returned null or empty embedding. Response: {Response}", responseBody);
            throw new InvalidOperationException("Gemini returned null or empty embedding");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding from Gemini");
            throw;
        }
    }

    public async Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken, List<GeminiTool>? tools = null)
    {
        try
        {
            _logger.LogInformation("Sending text prompt to Gemini");

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                ["generationConfig"] = new Dictionary<string, object>
                {
                    ["temperature"] = 0.0,
                    ["maxOutputTokens"] = 20000,
                    ["thinkingConfig"] = new { thinkingBudget = 0 }
                },
                ["safetySettings"] = Array.Empty<object>()
            };

            if (tools != null && tools.Count > 0)
            {
                requestBody["tools"] = tools;
            }

            string jsonBody = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);
            var completion = await PostToGeminiAsync(jsonBody, cancellationToken);

            if (string.IsNullOrWhiteSpace(completion))
            {
                _logger.LogWarning("Gemini returned an empty completion");
            }

            return completion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion from Gemini");
            throw;
        }
    }

    private async Task<string> PostToGeminiAsync(string jsonBody, CancellationToken cancellationToken)
    {
        var url = $"{_geminiEndpoint}?key={_apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        var client = _httpClientFactory.CreateClient(nameof(GeminiApiService));

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
            throw new Exception($"Gemini API error: {response.StatusCode}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody, _jsonSerializerOptions);

        if (geminiResponse == null || geminiResponse.Candidates == null || !geminiResponse.Candidates.Any())
        {
            _logger.LogWarning("Gemini API returned an empty response");
            return string.Empty;
        }

        var text = geminiResponse.Candidates.First().Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
        return RemoveCodeBlockFormatting(text);
    }

    public async Task<GeminiCompletionResult> GenerateCompletionWithToolsAsync(List<object> contents, List<GeminiTool>? tools, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sending request to Gemini with tools");

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = contents,
                ["generationConfig"] = new Dictionary<string, object>
                {
                    ["temperature"] = 0.0,
                    ["maxOutputTokens"] = 20000,
                    ["thinkingConfig"] = new { thinkingBudget = 2000 }
                },
                ["safetySettings"] = Array.Empty<object>()
            };

            if (tools != null && tools.Count > 0)
            {
                requestBody["tools"] = tools;
            }

            string jsonBody = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);
            var url = $"{_geminiEndpoint}?key={_apiKey}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            var client = _httpClientFactory.CreateClient(nameof(GeminiApiService));

            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Gemini API error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody, _jsonSerializerOptions);

            if (geminiResponse == null || geminiResponse.Candidates == null || !geminiResponse.Candidates.Any())
            {
                _logger.LogWarning("Gemini API returned an empty response");
                return new GeminiCompletionResult { Text = string.Empty, FunctionCalls = new List<GeminiFunctionCall>() };
            }

            var candidate = geminiResponse.Candidates.First();
            var parts = candidate.Content?.Parts ?? new List<GeminiPart>();
            
            var text = parts.FirstOrDefault(p => !string.IsNullOrEmpty(p.Text))?.Text ?? string.Empty;
            var functionCalls = parts
                .Where(p => p.FunctionCall != null)
                .Select(p => p.FunctionCall!)
                .ToList();

            return new GeminiCompletionResult
            {
                Text = RemoveCodeBlockFormatting(text),
                FunctionCalls = functionCalls,
                FinishReason = candidate.FinishReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion with tools from Gemini");
            throw;
        }
    }

    public async Task<string> GenerateMultimodalCompletionAsync(string prompt, byte[] imageData, string mimeType, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Sending multimodal prompt to Gemini with image");

            var base64Image = Convert.ToBase64String(imageData);

            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                ["generationConfig"] = new Dictionary<string, object>
                {
                    ["temperature"] = 0.0,
                    ["maxOutputTokens"] = 20000,
                    ["thinkingConfig"] = new { thinkingBudget = 0 }
                },
                ["safetySettings"] = Array.Empty<object>()
            };

            string jsonBody = JsonSerializer.Serialize(requestBody, _jsonSerializerOptions);
            var completion = await PostToGeminiAsync(jsonBody, cancellationToken);

            if (string.IsNullOrWhiteSpace(completion))
            {
                _logger.LogWarning("Gemini returned an empty completion for multimodal request");
            }

            return completion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating multimodal completion from Gemini");
            throw;
        }
    }

    private static string RemoveCodeBlockFormatting(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return response;
        }

        // Remove opening code block markers (e.g., ```json)
        if (response.StartsWith("```"))
        {
            int firstLineEnd = response.IndexOf('\n');
            if (firstLineEnd != -1)
            {
                response = response.Substring(firstLineEnd + 1);
            }
        }

        // Remove trailing code block markers
        int lastFenceIndex = response.LastIndexOf("```");
        if (lastFenceIndex != -1)
        {
            response = response.Substring(0, lastFenceIndex);
        }

        return response.Trim();
    }

    

    private static float[] NormalizeVector(float[] vector)
    {
        // Use double for intermediate sum to avoid overflow on large vectors
        double sumOfSquares = vector.Sum(x => (double)x * x);
        var norm = (float)Math.Sqrt(sumOfSquares);

        // CRITICAL: Handle the zero vector case
        if (norm == 0.0f)
        {
            // Return a new zero vector of the same length
            return new float[vector.Length]; 
            // Or you could throw an exception:
            // throw new InvalidOperationException("Cannot normalize a zero-length vector.");
        }

        return vector.Select(x => x / norm).ToArray();
    }

    // DTOs for embedding response
    private sealed class GeminiEmbeddingResponse
    {
        public GeminiEmbedding Embedding { get; set; } = new();
    }

    private sealed class GeminiEmbedding
    {
        public List<float> Values { get; set; } = new();
    }
}

