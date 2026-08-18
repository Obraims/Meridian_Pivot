namespace StockSync.Models;

public class SyncResult
{
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int ItemsChecked { get; set; }
    public int ItemsAdded { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsRemoved { get; set; }
    public int ChangesApplied { get; set; }
    public string? ErrorMessage { get; set; }
}
