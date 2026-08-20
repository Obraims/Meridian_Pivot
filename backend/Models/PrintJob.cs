namespace StockSync.Models;

public class PrintJob
{
    public string Id { get; set; } = string.Empty;
    public string AttendeeId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
