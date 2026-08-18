namespace StockSync.Repositories;

using StockSync.Models;

public interface IDestinationInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync();
    Task AddAsync(InventoryItem item);
    Task UpdateAsync(InventoryItem item);
    Task DeleteAsync(int id);
    Task ApplyChangesAsync(IEnumerable<InventoryItem> toAdd, IEnumerable<InventoryItem> toUpdate, IEnumerable<int> toDeleteIds);
}
