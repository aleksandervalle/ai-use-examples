using System.Text;
using System.Text.Json;
using AiUseExamples.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example4Controller : ControllerBase
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly ILogger<Example4Controller> _logger;

    public Example4Controller(IGeminiApiService geminiApiService, ILogger<Example4Controller> logger)
    {
        _geminiApiService = geminiApiService;
        _logger = logger;
    }

    [HttpPost("process-document")]
    public async Task ProcessDocument(IFormFile file, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        if (file == null || file.Length == 0)
        {
            await Response.WriteAsync("Error: No file uploaded", cancellationToken);
            return;
        }

        try
        {
            // Read file data
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            var imageData = memoryStream.ToArray();

            // Determine MIME type
            var mimeType = file.ContentType ?? GetMimeTypeFromFileName(file.FileName);

            // Step 1: Classify document type and generate alternative filename in parallel
            var classificationPrompt = @"Classify this image/document into one of these categories:
            - invoice
            - order_confirmation
            - flight_ticket
            - receipt
            - other_image

Respond with JSON only in this format:
{
  ""type"": ""<category from list above>"",
  ""confidence"": ""number"", // 0 means very low, 1 means very high
}

Return JSON only.";

            var filenamePrompt = $@"Based on the content of this image/document, suggest a short but descriptive alternative filename. It should include discriminative info like vendor name, store name etc, so that the user can easily identify the document. The original filename is: {file.FileName}. The new filename should have the same file extension as the original filename.

Respond with JSON only in this format:
{{
  ""alternativeFilename"": ""<suggested filename with same extension>""
}}

Return JSON only.";

            // Run classification and filename generation in parallel
            var classificationTask = _geminiApiService.GenerateMultimodalCompletionAsync(
                classificationPrompt, imageData, mimeType, cancellationToken);
            var filenameTask = _geminiApiService.GenerateMultimodalCompletionAsync(
                filenamePrompt, imageData, mimeType, cancellationToken);

            await Task.WhenAll(classificationTask, filenameTask);

            var classificationResult = await classificationTask;
            var filenameResult = await filenameTask;

            // Parse classification
            var classificationJson = JsonDocument.Parse(classificationResult);
            var documentType = classificationJson.RootElement.GetProperty("type").GetString() ?? "other_image";

            // Stream classification and filename results to client
            await Response.WriteAsync("CLASSIFICATION_RESULT:", cancellationToken);
            await Response.WriteAsync(classificationResult, cancellationToken);
            await Response.WriteAsync("\n\n", cancellationToken);
            
            await Response.WriteAsync("FILENAME_RESULT:", cancellationToken);
            await Response.WriteAsync(filenameResult, cancellationToken);
            await Response.WriteAsync("\n\n", cancellationToken);
            
            await Response.Body.FlushAsync(cancellationToken);

            // Step 2: Extract data based on document type
            var extractionPrompt = GetExtractionPrompt(documentType);
            
            // For extraction, we'll use the image again with specialized prompt
            var extractionResult = await _geminiApiService.GenerateMultimodalCompletionAsync(
                extractionPrompt, imageData, mimeType, cancellationToken);

            // Stream extraction result immediately
            await Response.WriteAsync(extractionResult, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document");
            await Response.WriteAsync($"Error: {ex.Message}", cancellationToken);
        }
    }

    private string GetExtractionPrompt(string documentType)
    {
        return documentType switch
        {
            "invoice" => @"Extract structured information from this invoice image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- invoiceNumber (string)
- invoiceDate (string, ISO 8601 format)
- dueDate (string, ISO 8601 format, if available)
- bankAccountNumber (string, if available)
- cid (string, organization number, if available)
- vendorName (string)
- customerName (string, if available)
- currency (string, ISO 4217 currency code like ""USD"", ""EUR"", ""NOK"", etc. If not explicitly stated, make a best guess based on location indicators, language, or other context clues)
- lineItems (array of objects with: description, quantity (number, if available), unitPrice (number), total (number))
- subtotal (number)
- tax (number, if available)
- total (number)

Return JSON only.",

            "flight_ticket" => @"Extract structured information from this flight ticket image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- travelingFrom (string, departure city/airport)
- travelingTo (string, destination city/airport)
- departureDate (string, ISO 8601 format)
- departureTime (string, time format)
- arrivalDate (string, ISO 8601 format, if available)
- arrivalTime (string, time format, if available)
- flightNumber (string, if available)
- passengerName (string, if available)
- bookingReference (string, if available)

Return JSON only.",

            "receipt" => @"Extract structured information from this receipt image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- storeName (string)
- transactionDate (string, ISO 8601 format)
- transactionTime (string, time format, if available)
- currency (string, ISO 4217 currency code like ""USD"", ""EUR"", ""NOK"", etc. If not explicitly stated, make a best guess based on location indicators, language, or other context clues)
- items (array of objects with: name, price (number), quantity (number, if available))
- subtotal (number, if available)
- tax (number, if available)
- total (number)
- paymentMethod (string, if available)

Return JSON only.",

            "order_confirmation" => @"Extract structured information from this order confirmation image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- orderNumber (string)
- orderDate (string, ISO 8601 format)
- currency (string, ISO 4217 currency code like ""USD"", ""EUR"", ""NOK"", etc. If not explicitly stated, make a best guess based on location indicators, language, or other context clues)
- items (array of objects with: name, quantity (number), price (number))
- subtotal (number)
- tax (number, if available)
- shipping (number, if available)
- total (number)

Return JSON only.",

            _ => @"Provide a detailed description of this image and suggest a short but descriptive alternative filename. Return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- description (string, detailed description of the image content)

Return JSON only."
        };
    }

    private string GetMimeTypeFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "image/jpeg" // Default fallback
        };
    }
}

