var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClientFactory for HTTP clients
builder.Services.AddHttpClient();

// Add Gemini API Service
builder.Services.AddScoped<AiUseExamples.Api.Services.IGeminiApiService, AiUseExamples.Api.Services.GeminiApiService>();

// Add Function Services
builder.Services.AddScoped<AiUseExamples.Api.Services.IWeatherService, AiUseExamples.Api.Services.WeatherService>();
builder.Services.AddScoped<AiUseExamples.Api.Services.IPersonLookupService, AiUseExamples.Api.Services.PersonLookupService>();
builder.Services.AddScoped<AiUseExamples.Api.Services.IMeetingService, AiUseExamples.Api.Services.MeetingService>();

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

app.Run();
