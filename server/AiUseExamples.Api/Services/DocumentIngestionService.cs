using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiUseExamples.Api.Data;
using AiUseExamples.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Services;

public class DocumentIngestionService
{
    private readonly DocumentStorageService _storage;
    private readonly DocumentsRepository _repo;
    private readonly IExtractionService _extraction;
    private readonly IGeminiApiService _gemini;
    private readonly IChromaDbService _chroma;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        DocumentStorageService storage,
        DocumentsRepository repo,
        IExtractionService extraction,
        IGeminiApiService gemini,
        IChromaDbService chroma,
        ILogger<DocumentIngestionService> logger)
    {
        _storage = storage;
        _repo = repo;
        _extraction = extraction;
        _gemini = gemini;
        _chroma = chroma;
        _logger = logger;
    }

    public async Task<Document> IngestAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Save temp copy to disk
        var stored = await _storage.SaveTemporaryAsync(file, id, cancellationToken);

        var document = new Document
        {
            Id = id,
            OriginalFileName = file.FileName,
            StoredFileName = stored.StoredFileName,
            FilePath = stored.FilePath,
            MimeType = stored.MimeType,
            FileSize = stored.FileSize,
            ProcessingStatus = "Processing",
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _repo.InsertAsync(document, cancellationToken);

            // Load bytes from saved file for multimodal prompts
            var imageData = await File.ReadAllBytesAsync(stored.FilePath, cancellationToken);

            // 1) Classification + Better Name
            var (docType, confidence, betterName) = await _extraction.ClassifyAndSuggestNameAsync(imageData, stored.MimeType, file.FileName, cancellationToken);

            // 2) Rename file on disk to final name
            var ext = Path.GetExtension(file.FileName);
            var shortId = id.ToString("N").Substring(0, 8);
            var datePart = now.ToString("yyyyMMdd");
            var safeStem = DocumentStorageService.Slugify(betterName);
            var finalFileName = $"{docType}-{datePart}-{safeStem}-{shortId}{ext}";
            var renameResult = _storage.Rename(stored.FilePath, finalFileName);

            await _repo.UpdateAfterClassificationAsync(id, renameResult.storedFileName, renameResult.filePath, docType, confidence, betterName, cancellationToken);

            // 3) Extraction
            var (extractedJson, description) = await _extraction.ExtractDataJsonAsync(docType, imageData, stored.MimeType, cancellationToken);
            await _repo.UpdateExtractionAsync(id, extractedJson, description, cancellationToken);

            // 4) Embedding input - include description in canonical text
            var canonicalText = betterName + "\nDocType: " + docType + "\nDescription: " + description + "\nExtractedData: " + extractedJson;
            var embedding = await _gemini.GenerateEmbeddingAsync(canonicalText, GeminiEmbeddingTaskType.RetrievalDocument, cancellationToken);

            // 5) Upsert to Chroma
            await _chroma.UpsertAsync(id, embedding, canonicalText, new { docType, betterName, filePath = renameResult.filePath, mimeType = stored.MimeType, fileSize = stored.FileSize, createdAt = now }, cancellationToken);

            // 6) Mark embedded
            await _repo.UpdateEmbeddedAsync(id, DateTime.UtcNow, cancellationToken);

            // Update in-memory object
            document.DocType = docType;
            document.ClassificationConfidence = confidence;
            document.BetterName = betterName;
            document.ExtractedDataJson = extractedJson;
            document.Description = description;
            document.StoredFileName = renameResult.storedFileName;
            document.FilePath = renameResult.filePath;
            document.EmbeddedAt = DateTime.UtcNow;
            document.ProcessingStatus = "Completed";
            document.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting document {Id}", id);
            await _repo.SetFailedAsync(id, ex.Message, cancellationToken);
            document.ProcessingStatus = "Failed";
            document.ErrorMessage = ex.Message;
            throw;
        }

        return document;
    }
}


