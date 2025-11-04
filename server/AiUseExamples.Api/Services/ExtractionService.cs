using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Services;

public interface IExtractionService
{
    Task<(string DocType, double Confidence, string BetterName)> ClassifyAndSuggestNameAsync(byte[] imageData, string mimeType, string originalFileName, CancellationToken cancellationToken);
    Task<(string ExtractedDataJson, string Description)> ExtractDataJsonAsync(string normalizedDocType, byte[] imageData, string mimeType, CancellationToken cancellationToken);
}

public class ExtractionService : IExtractionService
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(IGeminiApiService geminiApiService, ILogger<ExtractionService> logger)
    {
        _geminiApiService = geminiApiService;
        _logger = logger;
    }

    public async Task<(string DocType, double Confidence, string BetterName)> ClassifyAndSuggestNameAsync(byte[] imageData, string mimeType, string originalFileName, CancellationToken cancellationToken)
    {
        var classificationPrompt = @"Classify this image/document into one of these categories (exact string values):
- Invoice
- Receipt
- Flight Ticket
- Order Confirmation
- Other

Respond with JSON only in this format:
{
  ""docType"": ""<one of the categories above>"",
  ""confidence"": <number between 0 and 1>
}

Return JSON only.";

        var filenamePrompt = $@"Based on the content of this image/document, suggest a short descriptive filename stem (no extension). Include discriminative info such as vendor/store, destination, order number, etc. The original filename is: {originalFileName}.

Respond with JSON only in this format:
{{
  ""betterName"": ""<concise English filename stem without extension>""
}}

Return JSON only.";

        var classificationTask = _geminiApiService.GenerateMultimodalCompletionAsync(classificationPrompt, imageData, mimeType, cancellationToken);
        var filenameTask = _geminiApiService.GenerateMultimodalCompletionAsync(filenamePrompt, imageData, mimeType, cancellationToken);

        await Task.WhenAll(classificationTask, filenameTask);

        var classificationResult = await classificationTask;
        var filenameResult = await filenameTask;

        var (docType, confidence) = ParseClassification(classificationResult);
        var betterName = ParseBetterName(filenameResult);

        return (NormalizeDocType(docType), confidence, betterName);
    }

    public async Task<(string ExtractedDataJson, string Description)> ExtractDataJsonAsync(string normalizedDocType, byte[] imageData, string mimeType, CancellationToken cancellationToken)
    {
        var extractionPrompt = GetExtractionPrompt(normalizedDocType);
        var descriptionPrompt = @"Provide a detailed description of what this document is about. Focus on the key information, purpose, and context. Be specific and informative.

Respond with JSON only in this format:
{
  ""description"": ""<detailed description of the document>""
}

Return JSON only.";

        var extractionTask = _geminiApiService.GenerateMultimodalCompletionAsync(extractionPrompt, imageData, mimeType, cancellationToken);
        var descriptionTask = _geminiApiService.GenerateMultimodalCompletionAsync(descriptionPrompt, imageData, mimeType, cancellationToken);

        await Task.WhenAll(extractionTask, descriptionTask);

        var extractedJson = await extractionTask;
        var descriptionResult = await descriptionTask;

        var description = ParseDescription(descriptionResult);

        return (extractedJson, description);
    }

    private static string ParseDescription(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("description", out var desc))
            {
                return desc.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // If parsing fails, return empty string
        }
        return string.Empty;
    }

    private static (string docType, double confidence) ParseClassification(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var docType = root.TryGetProperty("docType", out var t) ? (t.GetString() ?? "Other") : root.TryGetProperty("type", out var t2) ? (t2.GetString() ?? "Other") : "Other";
        var confidence = root.TryGetProperty("confidence", out var c) ? TryGetDouble(c) : 0.0;
        return (docType, confidence);
    }

    private static string ParseBetterName(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("betterName", out var b))
        {
            return b.GetString() ?? "document";
        }
        if (root.TryGetProperty("alternativeFilename", out var a))
        {
            var fn = a.GetString() ?? "document";
            var withoutExt = fn;
            var idx = fn.LastIndexOf('.')
;            if (idx > 0) withoutExt = fn.Substring(0, idx);
            return withoutExt;
        }
        return "document";
    }

    private static double TryGetDouble(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetDouble(out var d) ? d : 0.0,
            JsonValueKind.String => double.TryParse(el.GetString(), out var d2) ? d2 : 0.0,
            _ => 0.0
        };
    }

    private static string NormalizeDocType(string docType)
    {
        var t = (docType ?? string.Empty).Trim().ToLowerInvariant();
        return t switch
        {
            "invoice" => "Invoice",
            "receipt" => "Receipt",
            "flight_ticket" or "flight ticket" or "ticket" => "Flight Ticket",
            "order_confirmation" or "order confirmation" => "Order Confirmation",
            _ => "Other"
        };
    }

    private static string GetExtractionPrompt(string normalizedDocType)
    {
        switch (normalizedDocType)
        {
            case "Invoice":
                return @"Extract structured information from this invoice image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

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

Return JSON only.";

            case "Flight Ticket":
                return @"Extract structured information from this flight ticket image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

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

Return JSON only.";

            case "Receipt":
                return @"Extract structured information from this receipt image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

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

Return JSON only.";

            case "Order Confirmation":
                return @"Extract structured information from this order confirmation image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- orderNumber (string)
- orderDate (string, ISO 8601 format)
- currency (string, ISO 4217 currency code like ""USD"", ""EUR"", ""NOK"", etc. If not explicitly stated, make a best guess based on location indicators, language, or other context clues)
- items (array of objects with: name, quantity (number), price (number))
- subtotal (number)
- tax (number, if available)
- shipping (number, if available)
- total (number)

Return JSON only.";

            default:
                return @"Provide a detailed description of this image and return it as valid JSON only. Do not include any explanation or markdown formatting, just the JSON object.

Extract the following fields:
- description (string, detailed description of the image content)

Return JSON only.";
        }
    }
}


