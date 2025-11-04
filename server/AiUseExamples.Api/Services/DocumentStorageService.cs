using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiUseExamples.Api.Models;
using AiUseExamples.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AiUseExamples.Api.Services;

public class DocumentStorageService
{
    private readonly DocumentStorageOptions _options;

    public DocumentStorageService(IOptions<DocumentStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredFileInfo> SaveTemporaryAsync(IFormFile file, Guid documentId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.RootPath);

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".bin";
        }

        var storedFileName = documentId.ToString("N") + extension.ToLowerInvariant();
        var filePath = Path.Combine(_options.RootPath, storedFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var info = new FileInfo(filePath);
        return new StoredFileInfo
        {
            StoredFileName = storedFileName,
            FilePath = filePath,
            FileSize = info.Length,
            MimeType = file.ContentType ?? GetMimeTypeFromExtension(extension)
        };
    }

    public (string storedFileName, string filePath) Rename(string currentFilePath, string newFileNameWithExtension)
    {
        var directory = Path.GetDirectoryName(currentFilePath) ?? _options.RootPath;
        var safeName = Slugify(Path.GetFileNameWithoutExtension(newFileNameWithExtension));
        var extension = Path.GetExtension(newFileNameWithExtension);
        var finalName = safeName + extension.ToLowerInvariant();

        var newPath = Path.Combine(directory, finalName);
        if (!File.Exists(currentFilePath))
        {
            throw new FileNotFoundException("Source file not found for rename", currentFilePath);
        }

        if (!string.Equals(currentFilePath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(newPath))
            {
                // Avoid collision by appending short id
                var shortId = Guid.NewGuid().ToString("N").Substring(0, 8);
                finalName = safeName + "-" + shortId + extension.ToLowerInvariant();
                newPath = Path.Combine(directory, finalName);
            }
            File.Move(currentFilePath, newPath);
        }

        return (finalName, newPath);
    }

    public static string Slugify(string input)
    {
        input = input.ToLowerInvariant();
        input = Regex.Replace(input, @"[^a-z0-9\s-]", "");
        input = Regex.Replace(input, @"[\s-]+", " ").Trim();
        input = Regex.Replace(input, @"\s", "-");
        return input;
    }

    private static string GetMimeTypeFromExtension(string extension)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".png": return "image/png";
            case ".gif": return "image/gif";
            case ".webp": return "image/webp";
            case ".pdf": return "application/pdf";
            default: return "application/octet-stream";
        }
    }
}


