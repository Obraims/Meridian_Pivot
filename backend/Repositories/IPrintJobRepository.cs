namespace StockSync.Repositories;

using StockSync.Models;

public interface IPrintJobRepository
{
    Task CreateAsync(PrintJob job);
    Task<PrintJob?> GetByIdAsync(string id);
    Task<IReadOnlyList<PrintJob>> GetAllAsync();
    Task UpdateStatusAsync(string id, string status, DateTime? completedAt);
    Task ResetAllAsync();
}
