namespace StockSync.Models;

public class CheckinResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? PrintJobId { get; set; }
    public string? AttendeeId { get; set; }
    public string? AttendeeName { get; set; }
}
