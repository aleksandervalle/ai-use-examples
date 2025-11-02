using Microsoft.Extensions.Logging;

namespace AiUseExamples.Api.Services;

public interface IMeetingService
{
    Task<MeetingScheduleResult> ScheduleMeetingAsync(List<string> attendeeEmails, string date, string time, string agenda);
}

public class MeetingService : IMeetingService
{
    private readonly ILogger<MeetingService> _logger;

    public MeetingService(ILogger<MeetingService> logger)
    {
        _logger = logger;
    }

    public Task<MeetingScheduleResult> ScheduleMeetingAsync(List<string> attendeeEmails, string date, string time, string agenda)
    {
        _logger.LogInformation("Scheduling meeting for {Date} at {Time} with {Count} attendees. Agenda: {Agenda}", 
            date, time, attendeeEmails.Count, agenda);

        // Mock implementation - in real scenario, this would create the meeting in a calendar system
        var result = new MeetingScheduleResult
        {
            Success = true,
            Message = "Meeting scheduled successfully",
            MeetingId = Guid.NewGuid().ToString(),
            Attendees = attendeeEmails,
            Date = date,
            Time = time,
            Agenda = agenda
        };

        return Task.FromResult(result);
    }
}

public class MeetingScheduleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public List<string> Attendees { get; set; } = new();
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
}

