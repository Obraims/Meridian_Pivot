namespace StockSync.Repositories;

using StockSync.Models;

public interface ISyncHistoryRepository
{
    Task<int> AddAsync(SyncHistory record);
    Task<List<SyncHistory>> GetAllAsync();
}
