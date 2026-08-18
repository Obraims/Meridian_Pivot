namespace StockSync.Services;

using StockSync.Models;
using StockSync.Repositories;

public class InventorySyncService : IInventorySyncService
{
    private readonly ISourceInventoryRepository _sourceRepo;
    private readonly IDestinationInventoryRepository _destinationRepo;
    private readonly ISyncHistoryRepository _syncHistoryRepo;

    public InventorySyncService(
        ISourceInventoryRepository sourceRepo,
        IDestinationInventoryRepository destinationRepo,
        ISyncHistoryRepository syncHistoryRepo)
    {
        _sourceRepo = sourceRepo;
        _destinationRepo = destinationRepo;
        _syncHistoryRepo = syncHistoryRepo;
    }

    public async Task<SyncResult> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;

        try
        {
            var sourceItems = await _sourceRepo.GetAllAsync();
            var destinationItems = await _destinationRepo.GetAllAsync();

            var destinationLookup = destinationItems
                .GroupBy(d => d.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var sourceLookup = sourceItems
                .GroupBy(s => s.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<InventoryItem>();
            var toUpdate = new List<InventoryItem>();
            var toDeleteIds = new List<int>();

            // Check source items against destination (Case A & Case B)
            foreach (var sourceItem in sourceItems)
            {
                if (destinationLookup.TryGetValue(sourceItem.ProductName, out var destItem))
                {
                    // Case B: Product exists in both but quantity differs
                    if (destItem.Quantity != sourceItem.Quantity)
                    {
                        toUpdate.Add(new InventoryItem
                        {
                            Id = destItem.Id,
                            ProductName = destItem.ProductName,
                            Quantity = sourceItem.Quantity,
                            UpdatedAt = startedAt
                        });
                    }
                }
                else
                {
                    // Case A: New product in source, not in destination
                    toAdd.Add(new InventoryItem
                    {
                        ProductName = sourceItem.ProductName,
                        Quantity = sourceItem.Quantity,
                        UpdatedAt = startedAt
                    });
                }
            }

            // Check destination items against source (Case C: Product removed from source)
            foreach (var destItem in destinationItems)
            {
                if (!sourceLookup.ContainsKey(destItem.ProductName))
                {
                    toDeleteIds.Add(destItem.Id);
                }
            }

            // Apply changes transactionally to destination repository
            if (toAdd.Count > 0 || toUpdate.Count > 0 || toDeleteIds.Count > 0)
            {
                await _destinationRepo.ApplyChangesAsync(toAdd, toUpdate, toDeleteIds);
            }

            var completedAt = DateTime.UtcNow;
            var changesApplied = toAdd.Count + toUpdate.Count + toDeleteIds.Count;

            var result = new SyncResult
            {
                Success = true,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                ItemsChecked = sourceItems.Count,
                ItemsAdded = toAdd.Count,
                ItemsUpdated = toUpdate.Count,
                ItemsRemoved = toDeleteIds.Count,
                ChangesApplied = changesApplied,
                ErrorMessage = null
            };

            // Record success in sync history
            try
            {
                await _syncHistoryRepo.AddAsync(new SyncHistory
                {
                    SyncedAt = completedAt,
                    Status = "Success",
                    Details = $"Checked: {result.ItemsChecked}, Added: {result.ItemsAdded}, Updated: {result.ItemsUpdated}, Removed: {result.ItemsRemoved}, Changes: {result.ChangesApplied}"
                });
            }
            catch
            {
                // Sync history write failures should not fail the synchronization outcome if DB changes succeeded,
                // but rethrow if desired or let caller know
            }

            return result;
        }
        catch (Exception ex)
        {
            var completedAt = DateTime.UtcNow;

            // Attempt to record failed sync attempt
            try
            {
                await _syncHistoryRepo.AddAsync(new SyncHistory
                {
                    SyncedAt = completedAt,
                    Status = "Failed",
                    Details = $"Error: {ex.Message}"
                });
            }
            catch
            {
                // Suppress secondary history logging error
            }

            return new SyncResult
            {
                Success = false,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                ItemsChecked = 0,
                ItemsAdded = 0,
                ItemsUpdated = 0,
                ItemsRemoved = 0,
                ChangesApplied = 0,
                ErrorMessage = ex.Message
            };
        }
    }
}
