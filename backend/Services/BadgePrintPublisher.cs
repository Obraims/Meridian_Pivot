namespace StockSync.Services;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StockSync.Models;

public class BadgePrintPublisher : IBadgePrintPublisher
{
    private readonly IConfiguration _configuration;
    private readonly string _queueName;
    private readonly ILogger<BadgePrintPublisher> _logger;

    public BadgePrintPublisher(IConfiguration configuration, ILogger<BadgePrintPublisher> logger)
    {
        _configuration = configuration;
        _queueName = _configuration["RabbitMQ:QueueName"] ?? "badge-print-requests";
        _logger = logger;
    }

    public async Task PublishPrintRequestAsync(BadgePrintMessage message)
    {
        var factory = new ConnectionFactory();
        var amqpUrl = _configuration["RabbitMQ:Url"] ?? _configuration["CLOUDAMQP_URL"] ?? _configuration["RABBITMQ_URL"];

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

        _logger.LogInformation("Connecting to RabbitMQ to publish print job {PrintJobId} for attendee {AttendeeId}",
            message.PrintJobId, message.AttendeeId);

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
