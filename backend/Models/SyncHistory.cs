namespace StockSync.Models;

public class SyncHistory
{
    public int Id { get; set; }
    public DateTime SyncedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Details { get; set; }
}
