namespace StockSync.Services;

using StockSync.Models;

public record WebhookResult(bool Success, bool IsDuplicate, string Message);

public interface IWebhookService
{
    Task<WebhookResult> ProcessPrintCompletedAsync(PrintCompletedWebhookPayload payload);
}
