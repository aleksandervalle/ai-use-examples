using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiUseExamples.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example3Controller : ControllerBase
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly IWeatherService _weatherService;
    private readonly IPersonLookupService _personLookupService;
    private readonly IMeetingService _meetingService;
    private readonly ILogger<Example3Controller> _logger;

    public Example3Controller(
        IGeminiApiService geminiApiService,
        IWeatherService weatherService,
        IPersonLookupService personLookupService,
        IMeetingService meetingService,
        ILogger<Example3Controller> logger)
    {
        _geminiApiService = geminiApiService;
        _weatherService = weatherService;
        _personLookupService = personLookupService;
        _meetingService = meetingService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";

        var tools = CreateTools();
        var contents = ConvertMessagesToGeminiFormat(request.Messages);
        
        // Add system message with current timestamp
        var systemMessage = new
        {
            role = "user",
            parts = new[]
            {
                new { text = $"Current timestamp: {request.CurrentTimestamp}. Use this timestamp to extract the correct weather data from XML responses. Always answer in Norwegian." }
            }
        };
        contents.Insert(0, systemMessage);

        // Handle function calls in a loop until we get a final text response
        var maxIterations = 10; // Prevent infinite loops
        var iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;

            var result = await _geminiApiService.GenerateCompletionWithToolsAsync(contents, tools, cancellationToken);

            // If there are function calls, execute them
            if (result.FunctionCalls.Any())
            {
                foreach (var functionCall in result.FunctionCalls)
                {
                    var functionResult = await ExecuteFunctionCall(functionCall, cancellationToken);
                    
                    // Add function response to conversation
                    contents.Add(new
                    {
                        role = "assistant",
                        parts = new[]
                        {
                            new
                            {
                                functionCall = new
                                {
                                    name = functionCall.Name,
                                    args = functionCall.Args
                                }
                            }
                        }
                    });

                    contents.Add(new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new
                            {
                                functionResponse = new
                                {
                                    name = functionCall.Name,
                                    response = functionResult
                                }
                            }
                        }
                    });
                }

                // Continue the conversation - don't write anything yet
                continue;
            }

            // No function calls - we have a final text response
            // Stream it word by word
            // LOL!
            if (!string.IsNullOrEmpty(result.Text))
            {
                var words = result.Text.Split(' ');
                foreach (var word in words)
                {
                    await Response.WriteAsync(word + " ", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    await Task.Delay(50, cancellationToken); // Small delay for streaming effect
                }
            }

            break;
        }

        if (iteration >= maxIterations)
        {
            await Response.WriteAsync("Error: Maximum iterations reached. There may be an issue with function calls.", cancellationToken);
        }
    }

    private async Task<object> ExecuteFunctionCall(GeminiFunctionCall functionCall, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing function call: {FunctionName}", functionCall.Name);

        return functionCall.Name switch
        {
            "get_weather" => await ExecuteGetWeather(functionCall.Args),
            "lookup_persons" => await ExecuteLookupPersons(functionCall.Args),
            "schedule_meeting" => await ExecuteScheduleMeeting(functionCall.Args),
            _ => throw new NotSupportedException($"Function {functionCall.Name} is not supported")
        };
    }

    private async Task<object> ExecuteGetWeather(Dictionary<string, object> args)
    {
        _logger.LogInformation("Executing get_weather function call with args: {Args}", args);
        var lat = ExtractDouble(args, "lat");
        var lon = ExtractDouble(args, "lon");
        var altitude = args.ContainsKey("altitude") ? ExtractInt(args, "altitude") : 10;

        var xml = await _weatherService.GetWeatherAsync(lat, lon, altitude);
        return new { xml };
    }

    private async Task<object> ExecuteLookupPersons(Dictionary<string, object> args)
    {
        var namesArray = args.GetValueOrDefault("names", new List<object>());
        var names = namesArray is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array
            ? jsonElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
            : ((List<object>)namesArray).Select(o => o.ToString() ?? string.Empty).ToList();

        var persons = await _personLookupService.LookupPersonsAsync(names);
        return new { persons = persons.Select(p => new { fullName = p.FullName, email = p.Email }).ToList() };
    }

    private async Task<object> ExecuteScheduleMeeting(Dictionary<string, object> args)
    {
        var emailsArray = args.GetValueOrDefault("attendeeEmails", new List<object>());
        var emails = emailsArray is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array
            ? jsonElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
            : ((List<object>)emailsArray).Select(o => o.ToString() ?? string.Empty).ToList();

        var date = ExtractString(args, "date");
        var time = ExtractString(args, "time");
        var agenda = ExtractString(args, "agenda");

        var result = await _meetingService.ScheduleMeetingAsync(emails, date, time, agenda);

        var responseHint = @"
        Please provide the user with a full meeting summary, including attendees (with full name and email), date and time, and agenda. Format like this:
        Meeting title: {agenda}
        Attendees: {attendees}
        Date: {date}
        Time: {time}
        ";

        return new { success = result.Success, message = result.Message + responseHint };
    }

    private double ExtractDouble(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out var value))
            return 0;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
                return jsonElement.GetDouble();
            if (jsonElement.ValueKind == JsonValueKind.String && double.TryParse(jsonElement.GetString(), out var parsed))
                return parsed;
        }

        return Convert.ToDouble(value);
    }

    private int ExtractInt(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out var value))
            return 0;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
                return jsonElement.GetInt32();
            if (jsonElement.ValueKind == JsonValueKind.String && int.TryParse(jsonElement.GetString(), out var parsed))
                return parsed;
        }

        return Convert.ToInt32(value);
    }

    private string ExtractString(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out var value))
            return string.Empty;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.GetString() ?? string.Empty;
        }

        return value?.ToString() ?? string.Empty;
    }

    private List<GeminiTool> CreateTools()
    {
        return new List<GeminiTool>
        {
            new GeminiTool
            {
                FunctionDeclarations = new List<GeminiFunctionDeclaration>
                {
                    new GeminiFunctionDeclaration
                    {
                        Name = "get_weather",
                        Description = "Get current weather forecast for a location. Returns XML data from Norwegian Meteorological Institute. Please use your best judgement to decide latitude and longitude for given location. Dont ask user for latitude and longitude, just use your best judgement.",
                        Parameters = new GeminiFunctionParameterSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, GeminiFunctionParameterProperty>
                            {
                                ["lat"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "number",
                                    Description = "Latitude of the location"
                                },
                                ["lon"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "number",
                                    Description = "Longitude of the location"
                                },
                                ["altitude"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "number",
                                    Description = "Altitude in meters (default: 10)"
                                }
                            },
                            Required = new List<string> { "lat", "lon" }
                        }
                    },
                    new GeminiFunctionDeclaration
                    {
                        Name = "lookup_persons",
                        Description = "Look up full names and email addresses for given name strings. Use this before scheduling meetings to get email addresses. Do NOT ask for surnames or other information. Simply input partial names if you get those from user.",
                        Parameters = new GeminiFunctionParameterSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, GeminiFunctionParameterProperty>
                            {
                                ["names"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "array",
                                    Description = "Array of name strings to look up (e.g., ['aleks', 'erik'])",
                                    Items = new GeminiFunctionParameterProperty { Type = "string" }
                                }
                            },
                            Required = new List<string> { "names" }
                        }
                    },
                    new GeminiFunctionDeclaration
                    {
                        Name = "schedule_meeting",
                        Description = "Schedule a meeting with specified attendees at a given time and date.",
                        Parameters = new GeminiFunctionParameterSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, GeminiFunctionParameterProperty>
                            {
                                ["attendeeEmails"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "array",
                                    Description = "Array of attendee email addresses",
                                    Items = new GeminiFunctionParameterProperty { Type = "string" }
                                },
                                ["date"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "Date of the meeting (e.g., '2024-07-29')"
                                },
                                ["time"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "Time of the meeting (e.g., '15:00')"
                                },
                                ["agenda"] = new GeminiFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The subject or topic of the meeting"
                                }
                            },
                            Required = new List<string> { "attendeeEmails", "date", "time", "agenda" }
                        }
                    }
                }
            }
        };
    }

    private List<object> ConvertMessagesToGeminiFormat(List<ChatMessage> messages)
    {
        return messages.Select(m => new
        {
            role = m.Role == "user" ? "user" : "model",
            parts = new[]
            {
                new { text = m.Content }
            }
        }).Cast<object>().ToList();
    }
}

public class ChatRequest
{
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("currentTimestamp")]
    public string CurrentTimestamp { get; set; } = string.Empty;
}

public class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

