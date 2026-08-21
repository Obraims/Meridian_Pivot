namespace StockSync.Services;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StockSync.Models;

public class MockBadgePrinterWorker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MockBadgePrinterWorker> _logger;

    public MockBadgePrinterWorker(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<MockBadgePrinterWorker> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = _configuration["RabbitMQ:QueueName"] ?? "badge-print-requests";
        var amqpUrl = _configuration["RabbitMQ:Url"] ?? _configuration["CLOUDAMQP_URL"] ?? _configuration["RABBITMQ_URL"];

        var factory = new ConnectionFactory();
        if (!string.IsNullOrWhiteSpace(amqpUrl))
        {
            factory.Uri = new Uri(amqpUrl);
        }
        else
        {
            factory.HostName = _configuration["RabbitMQ:Host"] ?? "localhost";
            factory.Port = int.TryParse(_configuration["RabbitMQ:Port"], out var p) ? p : 5672;
            if (!string.IsNullOrWhiteSpace(_configuration["RabbitMQ:Username"]))
            {
                factory.UserName = _configuration["RabbitMQ:Username"];
                factory.Password = _configuration["RabbitMQ:Password"] ?? string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(_configuration["RabbitMQ:VirtualHost"]))
            {
                factory.VirtualHost = _configuration["RabbitMQ:VirtualHost"];
            }
        }

        _logger.LogInformation("MockBadgePrinterWorker starting and connecting to RabbitMQ...");

        IConnection? connection = null;
        IChannel? channel = null;

        // Resilient connection loop
        while (!stoppingToken.IsCancellationRequested && connection == null)
        {
            try
            {
                connection = await factory.CreateConnectionAsync(stoppingToken);
                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken
                );

                _logger.LogInformation("MockBadgePrinterWorker connected to RabbitMQ and declared queue '{Queue}'.", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ connection attempt failed: {Message}. Retrying in 3 seconds...", ex.Message);
                await Task.Delay(3000, stoppingToken);
            }
        }

        if (channel == null || connection == null) return;

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                _logger.LogInformation("[MOCK PRINTER] Received badge print message from RabbitMQ: {Json}", json);

                var message = JsonSerializer.Deserialize<BadgePrintMessage>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (message != null)
                {
                    _logger.LogInformation("[MOCK PRINTER] Simulating physical printing for {Name} (Job: {JobId}, Attendee: {AttendeeId})...",
                        message.AttendeeName, message.PrintJobId, message.AttendeeId);

                    // Simulate hardware print time
                    await Task.Delay(1000, stoppingToken);

                    _logger.LogInformation("[MOCK PRINTER] Printing finished for {Name}. Dispatching webhook callback...", message.AttendeeName);

                    var payload = new PrintCompletedWebhookPayload
                    {
                        EventId = $"evt-{Guid.NewGuid():N}",
                        PrintJobId = message.PrintJobId,
                        AttendeeId = message.AttendeeId,
                        Status = "completed"
                    };

                    // Execute webhook callback via service scope
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookService>();
                        var result = await webhookService.ProcessPrintCompletedAsync(payload);
                        _logger.LogInformation("[MOCK PRINTER] Webhook callback handled with result: {Result}", result.Message);
                    }
                }

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MOCK PRINTER] Failed to process badge print message.");
            }
        };

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MockBadgePrinterWorker is shutting down.");
        }
        finally
        {
            if (channel != null) await channel.CloseAsync();
            if (connection != null) await connection.CloseAsync();
        }
    }
}
