using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiUseExamples.Api.Data;
using AiUseExamples.Api.Models;
using AiUseExamples.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example5Controller : ControllerBase
{
    private readonly DocumentIngestionService _ingestion;
    private readonly SearchService _search;
    private readonly DocumentsRepository _repo;
    private readonly IChromaDbService _chroma;
    private readonly ILogger<Example5Controller> _logger;

    public Example5Controller(DocumentIngestionService ingestion, SearchService search, DocumentsRepository repo, IChromaDbService chroma, ILogger<Example5Controller> logger)
    {
        _ingestion = ingestion;
        _search = search;
        _repo = repo;
        _chroma = chroma;
        _logger = logger;
    }

    [HttpPost("upload-documents")]
    [RequestSizeLimit(52428800)] // 50 MB safety cap; actual limit configured in Program
    public async Task<IActionResult> UploadDocuments([FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { error = "No files uploaded" });
        }

        var results = new List<object>();
        foreach (var file in files)
        {
            try
            {
                var doc = await _ingestion.IngestAsync(file, cancellationToken);
                results.Add(new { docId = doc.Id, originalFileName = doc.OriginalFileName, status = doc.ProcessingStatus });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting file {Name}", file.FileName);
                results.Add(new { docId = (Guid?)null, originalFileName = file.FileName, status = "Failed", error = ex.Message });
            }
        }

        return Ok(new { documents = results });
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required" });
        }
        var response = await _search.SearchAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("browse")]
    public async Task<IActionResult> Browse([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var (documents, totalCount) = await _repo.GetPaginatedAsync(page, pageSize, cancellationToken);

        var results = documents.Select(doc => new
        {
            docId = doc.Id,
            betterName = doc.BetterName ?? doc.OriginalFileName,
            docType = doc.DocType ?? "",
            mimeType = doc.MimeType,
            previewUrl = $"/api/Example5/documents/{doc.Id}/content",
            createdAt = doc.CreatedAt
        }).ToList();

        return Ok(new
        {
            results,
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            }
        });
    }

    [HttpGet("documents/{id}")]
    public async Task<IActionResult> GetDocument([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var doc = await _repo.GetByIdAsync(id, cancellationToken);
        if (doc == null) return NotFound();
        return Ok(new
        {
            doc.Id,
            doc.OriginalFileName,
            doc.StoredFileName,
            doc.FilePath,
            doc.MimeType,
            doc.FileSize,
            doc.DocType,
            doc.ClassificationConfidence,
            doc.BetterName,
            doc.ExtractedDataJson,
            doc.ProcessingStatus,
            doc.ErrorMessage,
            doc.EmbeddedAt,
            doc.CreatedAt,
            doc.UpdatedAt
        });
    }

    [HttpGet("documents/{id}/content")]
    public async Task<IActionResult> GetDocumentContent([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var doc = await _repo.GetByIdAsync(id, cancellationToken);
        if (doc == null) return NotFound();
        if (!System.IO.File.Exists(doc.FilePath))
        {
            return NotFound();
        }
        var contentType = doc.MimeType ?? "application/octet-stream";
        var fileName = string.IsNullOrWhiteSpace(doc.StoredFileName) ? doc.OriginalFileName : doc.StoredFileName;
        
        // Set Content-Disposition to "inline" so PDFs and images display in browser instead of downloading
        Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
        
        return PhysicalFile(doc.FilePath, contentType);
    }

    [HttpDelete("documents/{id}")]
    public async Task<IActionResult> DeleteDocument([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var doc = await _repo.GetByIdAsync(id, cancellationToken);
        if (doc == null) return NotFound();

        try
        {
            // Delete from ChromaDB
            await _chroma.DeleteAsync(new[] { id }, cancellationToken);

            // Delete file from disk if it exists
            if (!string.IsNullOrEmpty(doc.FilePath) && System.IO.File.Exists(doc.FilePath))
            {
                try
                {
                    System.IO.File.Delete(doc.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file {FilePath} for document {Id}", doc.FilePath, id);
                    // Continue with database deletion even if file deletion fails
                }
            }

            // Delete from SQLite
            var deleted = await _repo.DeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {Id}", id);
            return StatusCode(500, new { error = "Failed to delete document" });
        }
    }
}


