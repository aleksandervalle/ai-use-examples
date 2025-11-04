using System;

namespace AiUseExamples.Api.Models;

public class Document
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? DocType { get; set; }
    public double? ClassificationConfidence { get; set; }
    public string? BetterName { get; set; }
    public string? ExtractedDataJson { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? EmbeddedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}


