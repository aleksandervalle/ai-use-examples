using System;

namespace AiUseExamples.Api.Models;

public class StoredFileInfo
{
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}


