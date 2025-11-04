var builder = WebApplication.CreateBuilder(args);

// Bind options
builder.Services.Configure<AiUseExamples.Api.Options.DocumentStorageOptions>(builder.Configuration.GetSection("DocumentStorage"));
builder.Services.Configure<AiUseExamples.Api.Options.ChromaOptions>(builder.Configuration.GetSection("ChromaDb"));
builder.Services.Configure<AiUseExamples.Api.Options.LimitsOptions>(builder.Configuration.GetSection("Limits"));

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClientFactory for HTTP clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient(AiUseExamples.Api.Services.ChromaDbService.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiUseExamples.Api.Options.ChromaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseAddress);
});

// Add Gemini API Service (Singleton since it's consumed by singleton services and is stateless)
builder.Services.AddSingleton<AiUseExamples.Api.Services.IGeminiApiService, AiUseExamples.Api.Services.GeminiApiService>();

// Add Function Services
builder.Services.AddScoped<AiUseExamples.Api.Services.IWeatherService, AiUseExamples.Api.Services.WeatherService>();
builder.Services.AddScoped<AiUseExamples.Api.Services.IPersonLookupService, AiUseExamples.Api.Services.PersonLookupService>();
builder.Services.AddScoped<AiUseExamples.Api.Services.IMeetingService, AiUseExamples.Api.Services.MeetingService>();

// Example 5 services
builder.Services.AddSingleton<AiUseExamples.Api.Data.DocumentsRepository>();
builder.Services.AddSingleton<AiUseExamples.Api.Services.DocumentStorageService>();
builder.Services.AddSingleton<AiUseExamples.Api.Services.IExtractionService, AiUseExamples.Api.Services.ExtractionService>();
builder.Services.AddSingleton<AiUseExamples.Api.Services.IChromaDbService, AiUseExamples.Api.Services.ChromaDbService>();
builder.Services.AddSingleton<AiUseExamples.Api.Services.DocumentIngestionService>();
builder.Services.AddSingleton<AiUseExamples.Api.Services.SearchService>();

// Multipart limits
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    var limits = builder.Configuration.GetSection("Limits").Get<AiUseExamples.Api.Options.LimitsOptions>() ?? new AiUseExamples.Api.Options.LimitsOptions();
    options.MultipartBodyLengthLimit = (long)limits.MaxUploadSizeMb * 1024L * 1024L;
});

// Add CORS to allow requests from the React client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactClient", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowReactClient");

app.UseAuthorization();

app.MapControllers();

// Initialize schema and storage folder
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await AiUseExamples.Api.Data.SchemaInitializer.EnsureCreatedAsync(config, CancellationToken.None);
    var storageOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiUseExamples.Api.Options.DocumentStorageOptions>>().Value;
    System.IO.Directory.CreateDirectory(storageOptions.RootPath);
}

app.Run();
