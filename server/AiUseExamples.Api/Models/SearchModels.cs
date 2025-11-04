using System;
using System.Collections.Generic;

namespace AiUseExamples.Api.Models;

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int? TopK { get; set; }
    public string? DocType { get; set; }
}

public class SearchResultItem
{
    public Guid DocId { get; set; }
    public string BetterName { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public double Rerank { get; set; }
    public string PreviewUrl { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
}

public class SearchResponse
{
    public List<SearchResultItem> Results { get; set; } = new();
}


