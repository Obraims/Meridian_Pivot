namespace StockSync.Services;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StockSync.Models;

public class BadgePrintPublisher : IBadgePrintPublisher
{
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _queueName;
    private readonly ILogger<BadgePrintPublisher> _logger;

    public BadgePrintPublisher(IConfiguration configuration, ILogger<BadgePrintPublisher> logger)
    {
        _hostName = configuration["RabbitMQ:Host"] ?? "localhost";
        _port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        _queueName = configuration["RabbitMQ:QueueName"] ?? "badge-print-requests";
        _logger = logger;
    }

    public async Task PublishPrintRequestAsync(BadgePrintMessage message)
    {
        var factory = new ConnectionFactory
        {
            HostName = _hostName,
            Port = _port
        };

        _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port} to publish print job {PrintJobId} for attendee {AttendeeId}",
            _hostName, _port, message.PrintJobId, message.AttendeeId);

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: false,
            basicProperties: props,
            body: body
        );

        _logger.LogInformation("Successfully published print job {PrintJobId} to RabbitMQ queue {Queue}", message.PrintJobId, _queueName);
    }
}
