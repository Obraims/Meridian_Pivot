namespace StockSync.Services;

using Microsoft.Extensions.Logging;
using StockSync.Models;
using StockSync.Repositories;

public class CheckinService : ICheckinService
{
    private readonly IAttendeeRepository _attendeeRepo;
    private readonly IPrintJobRepository _printJobRepo;
    private readonly IBadgePrintPublisher _publisher;
    private readonly ILogger<CheckinService> _logger;

    public CheckinService(
        IAttendeeRepository attendeeRepo,
        IPrintJobRepository printJobRepo,
        IBadgePrintPublisher publisher,
        ILogger<CheckinService> logger)
    {
        _attendeeRepo = attendeeRepo;
        _printJobRepo = printJobRepo;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<CheckinResult> ProcessCheckinAsync(string attendeeId)
    {
        if (string.IsNullOrWhiteSpace(attendeeId))
        {
            return new CheckinResult
            {
                Success = false,
                Status = "INVALID_ID",
                Message = "Attendee ID must be provided."
            };
        }

        var attendee = await _attendeeRepo.GetByIdAsync(attendeeId.Trim());
        if (attendee == null)
        {
            _logger.LogWarning("Check-in attempt for non-existent attendee {AttendeeId}", attendeeId);
            return new CheckinResult
            {
                Success = false,
                Status = "NOT_FOUND",
                Message = $"Attendee '{attendeeId}' not found."
            };
        }

        // Duplicate-Scan Protection: Already Checked In
        if (attendee.Status == AttendeeStatus.CheckedIn)
        {
            _logger.LogInformation("Duplicate scan: Attendee {Id} ({Name}) is already CHECKED_IN. No new print job created.", attendee.Id, attendee.Name);
            return new CheckinResult
            {
                Success = true,
                Status = AttendeeStatus.CheckedIn,
                AttendeeId = attendee.Id,
                AttendeeName = attendee.Name,
                Message = $"Attendee {attendee.Name} is already checked in. Duplicate badge print avoided."
            };
        }

        // Duplicate-Scan Protection: Already Pending
        if (attendee.Status == AttendeeStatus.Pending)
        {
            _logger.LogInformation("Duplicate scan: Attendee {Id} ({Name}) is currently PENDING. No second print job created.", attendee.Id, attendee.Name);
            return new CheckinResult
            {
                Success = true,
                Status = AttendeeStatus.Pending,
                AttendeeId = attendee.Id,
                AttendeeName = attendee.Name,
                Message = $"Badge print for {attendee.Name} is already in progress."
            };
        }

        // State Transition: NOT_CHECKED_IN -> PENDING
        var now = DateTime.UtcNow;
        await _attendeeRepo.UpdateStatusAsync(attendee.Id, AttendeeStatus.Pending, now);

        var printJob = new PrintJob
        {
            Id = $"job-{Guid.NewGuid():N}",
            AttendeeId = attendee.Id,
            Status = "pending",
            CreatedAt = now
        };
        await _printJobRepo.CreateAsync(printJob);

        // Publish asynchronous print request to RabbitMQ
        var message = new BadgePrintMessage
        {
            PrintJobId = printJob.Id,
            AttendeeId = attendee.Id,
            AttendeeName = attendee.Name
        };
        await _publisher.PublishPrintRequestAsync(message);

        _logger.LogInformation("Check-in initiated for {Id} ({Name}). State set to PENDING, print job {JobId} queued to RabbitMQ.",
            attendee.Id, attendee.Name, printJob.Id);

        return new CheckinResult
        {
            Success = true,
            Status = AttendeeStatus.Pending,
            PrintJobId = printJob.Id,
            AttendeeId = attendee.Id,
            AttendeeName = attendee.Name,
            Message = "Check-in initiated. Badge print request sent to queue."
        };
    }
}
