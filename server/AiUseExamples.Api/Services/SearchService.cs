using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiUseExamples.Api.Data;
using AiUseExamples.Api.Models;
using AiUseExamples.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiUseExamples.Api.Services;

public class SearchService
{
    private readonly IChromaDbService _chroma;
    private readonly DocumentsRepository _repo;
    private readonly IGeminiApiService _gemini;
    private readonly LimitsOptions _limits;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IChromaDbService chroma, DocumentsRepository repo, IGeminiApiService gemini, IOptions<LimitsOptions> limits, ILogger<SearchService> logger)
    {
        _chroma = chroma;
        _repo = repo;
        _gemini = gemini;
        _limits = limits.Value;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var topK = request.TopK ?? _limits.DefaultTopK;

        // 1) Query expansion
        var expansion = await ExpandQueryAsync(request.Query, request.DocType, cancellationToken);
        var expandedQuery = expansion.ExpandedEnglishQuery;
        var effectiveDocType = !string.IsNullOrEmpty(request.DocType) ? request.DocType : expansion.DocType;

        // 2) Embedding + Vector search
        var queryEmbedding = await _gemini.GenerateEmbeddingAsync(expandedQuery, GeminiEmbeddingTaskType.RetrievalQuery, cancellationToken);
        var chromaResult = await _chroma.QueryAsync(queryEmbedding, topK, 0, null, cancellationToken);

        var ids = chromaResult.Ids.Length > 0 ? chromaResult.Ids[0] : Array.Empty<string>();
        var distances = chromaResult.Distances.Length > 0 ? chromaResult.Distances[0] : Array.Empty<double>();

        // Similarity = 1 - distance
        var pairs = new List<(Guid id, double sim)>();
        for (int i = 0; i < ids.Length && i < distances.Length; i++)
        {
            if (Guid.TryParse(ids[i], out var gid))
            {
                pairs.Add((gid, 1.0 - distances[i]));
            }
        }

        if (pairs.Count == 0)
        {
            return new SearchResponse();
        }

        // Fetch docs metadata
        var docIds = pairs.Select(p => p.id).ToArray();
        var docs = await _repo.GetByIdsAsync(docIds, cancellationToken);
        var docMap = docs.ToDictionary(d => d.Id, d => d);

        // 3) Rerank in parallel
        var rerankScores = new ConcurrentDictionary<Guid, double>();
        using var semaphore = new SemaphoreSlim(_limits.RerankConcurrency);
        var tasks = new List<Task>();
        foreach (var pair in pairs)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    if (!docMap.TryGetValue(pair.id, out var d) || string.IsNullOrWhiteSpace(d.ExtractedDataJson))
                    {
                        rerankScores[pair.id] = 0.0;
                        return;
                    }

                    var prompt = $@"You are a reranker. Given a user query and a document's extracted structured data, return a single relevancy score between 0 and 1.

Respond with JSON only in this format:
{{ ""docId"": ""{pair.id}"", ""relevancy"": <number 0..1> }}

UserQuery: {request.Query}

ExtractedDataJson: {d.ExtractedDataJson}

Return JSON only.";

                    var json = await _gemini.GenerateCompletionAsync(prompt, cancellationToken, null);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var score = root.TryGetProperty("relevancy", out var r)
                        ? (r.ValueKind == JsonValueKind.Number ? r.GetDouble() : double.TryParse(r.GetString(), out var v) ? v : 0.0)
                        : 0.0;
                    rerankScores[pair.id] = Math.Max(0.0, Math.Min(1.0, score));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Rerank failed for doc {Id}", pair.id);
                    rerankScores[pair.id] = 0.0;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }
        await Task.WhenAll(tasks);

        // 4) Sort by rerank desc, tie-break if needed
        var ordered = pairs
            .Select(p => new { p.id, p.sim, rerank = rerankScores.TryGetValue(p.id, out var rr) ? rr : 0.0 })
            .OrderByDescending(x => x.rerank)
            .ThenByDescending(x => x.sim)
            .ToList();

        var topRerank = ordered.FirstOrDefault()?.rerank ?? 0.0;
        var tied = ordered.Where(x => Math.Abs(x.rerank - topRerank) < 0.0001 && x.rerank >= 0.99).Select(x => x.id).ToList();
        if (tied.Count >= 2)
        {
            try
            {
                var rankOrder = await TieBreakAsync(request.Query, tied, docMap, cancellationToken);
                var orderMap = rankOrder.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
                ordered = ordered.OrderBy(x => orderMap.ContainsKey(x.id) ? orderMap[x.id] : int.MaxValue).ThenByDescending(x => x.rerank).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tie-break failed");
            }
        }

        // 5) Build response
        var response = new SearchResponse();
        foreach (var x in ordered)
        {
            if (!docMap.TryGetValue(x.id, out var d)) continue;
            // if (!string.IsNullOrEmpty(effectiveDocType) && !string.Equals(d.DocType, effectiveDocType, StringComparison.OrdinalIgnoreCase))
            // {
            //     // If a filter was provided, skip mismatches
            //     continue;
            // }
            response.Results.Add(new SearchResultItem
            {
                DocId = d.Id,
                BetterName = d.BetterName ?? d.OriginalFileName,
                DocType = d.DocType ?? "",
                Similarity = x.sim,
                Rerank = x.rerank,
                PreviewUrl = $"/api/Example5/documents/{d.Id}/content",
                MimeType = d.MimeType
            });
        }

        return response;
    }

    private async Task<(string EnglishQuery, string ExpandedEnglishQuery, string? DocType)> ExpandQueryAsync(string query, string? docType, CancellationToken cancellationToken)
    {
        var prompt = @"You are a query expander. Given a user query, translate to English if needed, and produce a short expanded English variant. Also, assign a document type if evident: Invoice, Receipt, Flight Ticket, Order Confirmation, or leave empty if unknown.

Respond with JSON only:
{
  ""englishQuery"": ""<English translation>"",
  ""expandedEnglishQuery"": ""<Expanded English query>"",
  ""docType"": ""Invoice|Receipt|Flight Ticket|Order Confirmation|Other|"" // Leave docType as empty string if query does not hint at document type
}

Return JSON only.
UserQuery: " + query;

        var json = await _gemini.GenerateCompletionAsync(prompt, cancellationToken, null);
        _logger.LogInformation("Query expansion response: {Response}", json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var english = root.TryGetProperty("englishQuery", out var e) ? e.GetString() ?? query : query;
        var expanded = root.TryGetProperty("expandedEnglishQuery", out var ex) ? ex.GetString() ?? english : english;
        
        // Handle docType: use provided docType if not empty, otherwise use LLM response if not empty
        string? dt = null;
        if (!string.IsNullOrEmpty(docType))
        {
            dt = docType;
        }
        else if (root.TryGetProperty("docType", out var dtEl))
        {
            var dtValue = dtEl.GetString();
            if (!string.IsNullOrEmpty(dtValue))
            {
                dt = dtValue;
            }
        }
        
        return (english, expanded, dt);
    }

    private async Task<List<Guid>> TieBreakAsync(string userQuery, List<Guid> ids, System.Collections.Generic.IDictionary<Guid, Document> docMap, CancellationToken cancellationToken)
    {
        var candidates = ids
            .Where(id => docMap.ContainsKey(id))
            .Select(id => new { docId = id, extractedData = docMap[id].ExtractedDataJson ?? string.Empty })
            .ToList();

        var prompt = @$"Tie-break ranking. Given a user query and a list of candidates with extractedData, return an ordered list of docIds from most to least relevant. Respond with JSON array only, e.g., [""guid1"",""guid2""].

UserQuery: {userQuery}

Candidates: {JsonSerializer.Serialize(candidates)}";

        var json = await _gemini.GenerateCompletionAsync(prompt, cancellationToken, null);
        var order = new List<Guid>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (Guid.TryParse(el.GetString(), out var gid)) order.Add(gid);
                }
            }
        }
        catch
        {
            // ignore
        }
        return order;
    }
}


