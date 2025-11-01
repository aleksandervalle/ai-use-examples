using System.Text.Json.Serialization;
using AiUseExamples.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example1Controller : ControllerBase
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly ILogger<Example1Controller> _logger;

    public Example1Controller(IGeminiApiService geminiApiService, ILogger<Example1Controller> logger)
    {
        _geminiApiService = geminiApiService;
        _logger = logger;
    }

    [HttpPost("extract-invoice-data")]
    public async Task<IActionResult> ExtractInvoiceData(
        [FromBody] ExtractInvoiceDataRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are a data extraction assistant. Extract structured information from the following invoice text and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Invoice text:
{request.InvoiceText}

Extract the following fields:
- invoiceNumber (string)
- date (string, ISO 8601 format)
- vendorName (string)
- totalAmount (number)
- currency (string)
- lineItems (array of objects with: description, quantity, unitPrice, total)

Return JSON only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }


// Show: STORE: Rema 1000 Sentrumsgården -> just store name, no STORE: 
// Change paymentMethod -> card | cash | other. And show that this might not be enough--prompt tweaking
    [HttpPost("parse-receipt")]
    public async Task<IActionResult> ParseReceipt(
        [FromBody] ParseReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are a receipt parser. Extract structured information from the following receipt text and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Receipt text:
{request.ReceiptText}

Extract the following fields:
- storeName (string)
- transactionDate (string, ISO 8601 format)
- items (array of objects with: name, price, quantity)
- subtotal (number)
- tax (number)
- total (number)
- paymentMethod

Return JSON only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }

// Show no spacing line between products
// Put price for product 1 on same line as product 2
    [HttpPost("structure-product-descriptions")]
    public async Task<IActionResult> StructureProductDescriptions(
        [FromBody] StructureProductDescriptionsRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"You are a product data processor. Take the following unstructured product descriptions and convert them into a structured JSON array. Do not include any explanation or markdown formatting, just the JSON array.

Product descriptions:
{request.ProductDescriptions}

For each product, extract:
- name (string)
- category (string)
- price (number, extract from text if available, otherwise null)
- description (string, cleaned up)
- features (array of strings)

Return JSON array only.";

        var result = await _geminiApiService.GenerateCompletionAsync(prompt, cancellationToken);
        return Ok(new { result });
    }
}

public class ExtractInvoiceDataRequest
{
    [JsonPropertyName("invoiceText")]
    public string InvoiceText { get; set; } = string.Empty;
}

public class ParseReceiptRequest
{
    [JsonPropertyName("receiptText")]
    public string ReceiptText { get; set; } = string.Empty;
}

public class StructureProductDescriptionsRequest
{
    [JsonPropertyName("productDescriptions")]
    public string ProductDescriptions { get; set; } = string.Empty;
}
