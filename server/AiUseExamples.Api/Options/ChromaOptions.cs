namespace AiUseExamples.Api.Options;

public class ChromaOptions
{
    public string BaseAddress { get; set; } = "http://localhost:8000";
    public string TenantId { get; set; } = "default_tenant";
    public string DatabaseName { get; set; } = "default_db";
    public string Collection { get; set; } = "documents";
}


