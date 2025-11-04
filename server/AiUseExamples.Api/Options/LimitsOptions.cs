namespace AiUseExamples.Api.Options;

public class LimitsOptions
{
    public int MaxUploadSizeMb { get; set; } = 20;
    public int MaxFilesPerBatch { get; set; } = 20;
    public int RerankConcurrency { get; set; } = 10;
    public int DefaultTopK { get; set; } = 50;
}


