using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Services;

public interface IWeatherService
{
    Task<string> GetWeatherAsync(double lat, double lon, int altitude = 10);
}

public class WeatherService : IWeatherService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(IHttpClientFactory httpClientFactory, ILogger<WeatherService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetWeatherAsync(double lat, double lon, int altitude = 10)
    {
        try
        {
            // Use InvariantCulture to ensure decimal separator is always a period (.)
            var url = $"https://api.met.no/weatherapi/locationforecast/2.0/classic?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lon.ToString(CultureInfo.InvariantCulture)}&altitude={altitude}";
            
            var client = _httpClientFactory.CreateClient(nameof(WeatherService));
            
            // Met.no API requires a User-Agent header
            // Using a standard browser User-Agent format
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            
            var response = await client.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Met.no API error: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Met.no API error: {response.StatusCode} - {errorContent}");
            }
            
            var xmlContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Retrieved weather data for lat={Lat}, lon={Lon}", lat, lon);
            
            return xmlContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data");
            throw;
        }
    }
}

