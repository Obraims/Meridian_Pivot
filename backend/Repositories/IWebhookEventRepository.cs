namespace StockSync.Repositories;

public interface IWebhookEventRepository
{
    Task<bool> ExistsAsync(string eventId);
    Task RecordAsync(string eventId, string attendeeId, string status);
    Task ResetAllAsync();
}
