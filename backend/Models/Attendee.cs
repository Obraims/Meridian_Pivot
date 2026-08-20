namespace StockSync.Models;

public static class AttendeeStatus
{
    public const string NotCheckedIn = "NOT_CHECKED_IN";
    public const string Pending = "PENDING";
    public const string CheckedIn = "CHECKED_IN";
}

public class Attendee
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = AttendeeStatus.NotCheckedIn;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
