namespace StockSync.Services;

using StockSync.Models;

public interface IBadgePrintPublisher
{
    Task PublishPrintRequestAsync(BadgePrintMessage message);
}
