using System.Text.Json.Serialization;
using AiUseExamples.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example2Controller : ControllerBase
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly ILogger<Example2Controller> _logger;

    public Example2Controller(IGeminiApiService geminiApiService, ILogger<Example2Controller> logger)
    {
        _geminiApiService = geminiApiService;
        _logger = logger;
    }

    [HttpPost("classify-sentiment")]
    public async Task<IActionResult> ClassifySentiment(
        [FromBody] ClassifyTextRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are a sentiment analysis classifier.
Classify the following customer feedback text into one of these categories:
- Positive
- Neutral
- Negative

Customer feedback:
{request.Text}

Respond with JSON only in this format:
{{
  ""classification"": ""Positive"",
  ""confidence"": ""number"", // 0 means very low, 1 means very high
  ""reasoning"": ""Brief explanation""
}}

Return JSON only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }

    [HttpPost("classify-expense-type")]
    public async Task<IActionResult> ClassifyExpenseType(
        [FromBody] ClassifyTextRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are an expense classification system.
Classify the following expense description into one of these categories:
- Travel
- Meals
- Office Supplies
- Software
- Utilities
- Marketing
- Professional Services
- Other

Expense description:
{request.Text}

Respond with JSON only in this format:
{{
  ""classification"": ""Travel"",
  ""confidence"": ""number"", // 0 means very low, 1 means very high
  ""reasoning"": ""Brief explanation""
}}

Return JSON only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }

    [HttpPost("classify-transaction-category")]
    public async Task<IActionResult> ClassifyTransactionCategory(
        [FromBody] ClassifyJsonRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are a transaction classifier. 
Classify the following transaction JSON data into one of these categories: 
- Income
- Expense
- Transfer
- Investment
- Refund

Transaction data:
{request.JsonData}

Respond with JSON only in this format:
{{
  ""classification"": ""Expense"",
  ""confidence"": ""number"", // 0 means very low, 1 means very high
  ""reasoning"": ""Brief explanation""
}}

Return JSON only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }

    [HttpPost("classify-ticket-priority")]
    public async Task<IActionResult> ClassifyTicketPriority(
        [FromBody] ClassifyJsonRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are a support ticket priority classifier.
Classify the following ticket JSON data into one of these priority levels:
- Critical
- High
- Medium
- Low

Ticket data:
{request.JsonData}

Respond with JSON only in this format:
{{
  ""classification"": ""High"",
  ""confidence"": ""number"", // 0 means very low, 1 means very high
  ""reasoning"": ""Brief explanation""
}}

Return JSON only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }
}

public class ClassifyTextRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class ClassifyJsonRequest
{
    [JsonPropertyName("jsonData")]
    public string JsonData { get; set; } = string.Empty;
}
