namespace StockSync.Repositories;

using StockSync.Models;

public interface ISourceInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync();
}
