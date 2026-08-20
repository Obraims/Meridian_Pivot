namespace StockSync.Services;

using Microsoft.Extensions.Logging;
using StockSync.Models;
using StockSync.Repositories;

public class WebhookService : IWebhookService
{
    private readonly IWebhookEventRepository _webhookEventRepo;
    private readonly IPrintJobRepository _printJobRepo;
    private readonly IAttendeeRepository _attendeeRepo;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IWebhookEventRepository webhookEventRepo,
        IPrintJobRepository printJobRepo,
        IAttendeeRepository attendeeRepo,
        ILogger<WebhookService> logger)
    {
        _webhookEventRepo = webhookEventRepo;
        _printJobRepo = printJobRepo;
        _attendeeRepo = attendeeRepo;
        _logger = logger;
    }

    public async Task<WebhookResult> ProcessPrintCompletedAsync(PrintCompletedWebhookPayload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.EventId))
        {
            return new WebhookResult(false, false, "EventId is required.");
        }

        if (string.IsNullOrWhiteSpace(payload.AttendeeId))
        {
            return new WebhookResult(false, false, "AttendeeId is required.");
        }

        // 1. Webhook Idempotency Check
        var exists = await _webhookEventRepo.ExistsAsync(payload.EventId);
        if (exists)
        {
            _logger.LogInformation("Duplicate webhook event {EventId} detected and safely ignored.", payload.EventId);
            return new WebhookResult(true, true, $"Event {payload.EventId} was already processed.");
        }

        // 2. Persist event in SQLite for persistent idempotency
        await _webhookEventRepo.RecordAsync(payload.EventId, payload.AttendeeId, payload.Status);

        var now = DateTime.UtcNow;

        // 3. Update Print Job if referenced
        if (!string.IsNullOrWhiteSpace(payload.PrintJobId))
        {
            await _printJobRepo.UpdateStatusAsync(payload.PrintJobId, "completed", now);
        }

        // 4. Safe State Transition for Attendee
        var attendee = await _attendeeRepo.GetByIdAsync(payload.AttendeeId);
        if (attendee != null)
        {
            if (attendee.Status == AttendeeStatus.Pending)
            {
                await _attendeeRepo.UpdateStatusAsync(attendee.Id, AttendeeStatus.CheckedIn, now);
                _logger.LogInformation("Attendee {Id} ({Name}) successfully transitioned PENDING -> CHECKED_IN via webhook {EventId}.",
                    attendee.Id, attendee.Name, payload.EventId);
            }
            else
            {
                _logger.LogInformation("Attendee {Id} has status {Status}. Webhook completion noted without corrupting state.",
                    attendee.Id, attendee.Status);
            }
        }

        return new WebhookResult(true, false, "Webhook processed successfully.");
    }
}
