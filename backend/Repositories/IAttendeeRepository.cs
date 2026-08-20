namespace StockSync.Repositories;

using StockSync.Models;

public interface IAttendeeRepository
{
    Task<Attendee?> GetByIdAsync(string id);
    Task<IReadOnlyList<Attendee>> GetAllAsync();
    Task UpdateStatusAsync(string id, string status, DateTime updatedAt);
    Task ResetAllAsync();
}
