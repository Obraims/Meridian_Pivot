namespace StockSync.Services;

using StockSync.Models;

public interface ICheckinService
{
    Task<CheckinResult> ProcessCheckinAsync(string attendeeId);
}
