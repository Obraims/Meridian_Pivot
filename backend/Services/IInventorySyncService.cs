namespace StockSync.Services;

using StockSync.Models;

public interface IInventorySyncService
{
    Task<SyncResult> SynchronizeAsync(CancellationToken cancellationToken = default);
}
