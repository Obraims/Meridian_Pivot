namespace StockSync.Models;

public class PrintCompletedWebhookPayload
{
    public string EventId { get; set; } = string.Empty;
    public string PrintJobId { get; set; } = string.Empty;
    public string AttendeeId { get; set; } = string.Empty;
    public string Status { get; set; } = "completed";
}
