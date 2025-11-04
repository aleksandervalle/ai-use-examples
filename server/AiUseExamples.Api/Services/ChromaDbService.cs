using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiUseExamples.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiUseExamples.Api.Services;

public interface IChromaDbService
{
    Task UpsertAsync(Guid id, float[] embedding, string? document, object? metadata, CancellationToken cancellationToken);
    Task<ChromaQueryResult> QueryAsync(float[] queryEmbedding, int nResults, int offset, string? containsFilter, CancellationToken cancellationToken);
    Task DeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken);
}

public class ChromaDbService : IChromaDbService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChromaDbService> _logger;
    private readonly ChromaOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public const string HttpClientName = nameof(ChromaDbService);

    public ChromaDbService(IHttpClientFactory httpClientFactory, IOptions<ChromaOptions> options, ILogger<ChromaDbService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task UpsertAsync(Guid id, float[] embedding, string? document, object? metadata, CancellationToken cancellationToken)
    {
        var url = $"/api/v2/tenants/{_options.TenantId}/databases/{_options.DatabaseName}/collections/{_options.Collection}/upsert";
        var request = new Dictionary<string, object?>
        {
            ["embeddings"] = new[] { embedding },
            ["ids"] = new[] { id.ToString() },
            ["metadatas"] = new object?[] { metadata }
        };
        if (!string.IsNullOrEmpty(document))
        {
            request["documents"] = new[] { document };
        }

        await SendAsync(HttpMethod.Post, url, request, cancellationToken);
    }

    public async Task<ChromaQueryResult> QueryAsync(float[] queryEmbedding, int nResults, int offset, string? containsFilter, CancellationToken cancellationToken)
    {
        var url = $"/api/v2/tenants/{_options.TenantId}/databases/{_options.DatabaseName}/collections/{_options.Collection}/query";
        var body = new Dictionary<string, object?>
        {
            ["query_embeddings"] = new[] { queryEmbedding },
            ["include"] = new[] { "distances" },
            ["n_results"] = nResults + Math.Max(0, offset)
        };
        if (!string.IsNullOrWhiteSpace(containsFilter))
        {
            body["where_document"] = new Dictionary<string, object?> { ["$contains"] = containsFilter };
        }

        var response = await SendAsync(HttpMethod.Post, url, body, cancellationToken);
        var result = JsonSerializer.Deserialize<ChromaQueryResult>(response, _jsonOptions) ?? new ChromaQueryResult();

        // Apply offset client-side if needed
        if (offset > 0 && result.Ids.Length > 0 && result.Distances.Length > 0)
        {
            var ids = result.Ids[0];
            var distances = result.Distances[0];
            if (offset < ids.Length)
            {
                result.Ids[0] = ids.Skip(offset).ToArray();
                result.Distances[0] = distances.Skip(offset).ToArray();
            }
            else
            {
                result.Ids[0] = Array.Empty<string>();
                result.Distances[0] = Array.Empty<double>();
            }
        }

        return result;
    }

    public async Task DeleteAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var arr = ids?.Select(x => x.ToString()).ToArray() ?? Array.Empty<string>();
        if (arr.Length == 0)
        {
            return;
        }
        var url = $"/api/v2/tenants/{_options.TenantId}/databases/{_options.DatabaseName}/collections/{_options.Collection}/delete";
        var body = new Dictionary<string, object> { ["ids"] = arr };
        await SendAsync(HttpMethod.Post, url, body, cancellationToken);
    }

    private async Task<string> SendAsync(HttpMethod method, string relativeUrl, object body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var json = JsonSerializer.Serialize(body, _jsonOptions);
        using var request = new HttpRequestMessage(method, relativeUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Chroma request failed {Status}: {Content}", response.StatusCode, err);
            throw new HttpRequestException($"Chroma request failed: {response.StatusCode}: {err}");
        }
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public class ChromaQueryResult
{
    public string[][] Ids { get; set; } = Array.Empty<string[]>();
    public double[][] Distances { get; set; } = Array.Empty<double[]>();
}


