# Learning & Blocker Journal: RabbitMQ & Asynchronous Architecture

## 1. Technology Learned: RabbitMQ in .NET 10

- **RabbitMQ.Client Version**: `7.2.2` (Modern fully asynchronous API).
- **Core Concepts Learned**:
  - **ConnectionFactory**: Configures AMQP endpoint (`HostName = "localhost"`, `Port = 5672`), credentials, and connection settings.
  - **IConnection & IChannel**: In `RabbitMQ.Client` 7.x, connection and channel lifecycle methods are asynchronous (`CreateConnectionAsync()`, `CreateChannelAsync()`).
  - **Queue Declaration**: Declaring durable queues via `channel.QueueDeclareAsync("badge-print-requests", durable: true, ...)`.
  - **Message Publishing**: Encoding domain models (`BadgePrintMessage`) to UTF-8 JSON byte arrays and setting `BasicProperties { DeliveryMode = DeliveryModes.Persistent }`.
  - **AsyncEventingBasicConsumer**: Consuming messages asynchronously, handling concurrency, simulating hardware processing, and calling `channel.BasicAckAsync()` to acknowledge message delivery.

---

## 2. Actual Setup Problems & Errors Encountered

1. **RabbitMQ Daemon Connectivity**:
   - *Problem*: Initial port test to `localhost:5672` failed because Docker Desktop Linux engine was not yet started.
   - *Fix*: Started Docker Desktop and launched the `meridian-rabbitmq` container mapping ports `5672` (AMQP) and `15672` (Management UI). Subsequent TCP verification confirmed port `5672` was listening.
2. **RabbitMQ.Client 7.x Async API Differences**:
   - *Problem*: Legacy documentation uses synchronous `channel.QueueDeclare()` and `channel.BasicPublish()`. In .NET 10 with RabbitMQ.Client 7.2.2, all methods are task-based (`QueueDeclareAsync`, `BasicPublishAsync`, `BasicAckAsync`).
   - *Fix*: Implemented `await using` asynchronous channel scopes and `AsyncEventingBasicConsumer` with `ReceivedAsync` delegates.
3. **Configuration Extension Method in Tests**:
   - *Problem*: `ConfigurationBuilder.AddInMemoryCollection` required package reference `Microsoft.Extensions.Configuration` and using directive.
   - *Fix*: Added `Microsoft.Extensions.Configuration` and `using Microsoft.Extensions.Configuration;`.

---

## 3. Approximate Time & Conceptual Takeaways

- **Time Spent**: ~1.5 hours on environment inspection, RabbitMQ package integration, domain modeling, worker implementation, and end-to-end testing.
- **Initial Confusion**: Managing connection lifecycle in ASP.NET Core background services.
- **Clarification**: Separating the publisher (`IBadgePrintPublisher` with transient/scoped connection usage) from the long-running worker (`MockBadgePrinterWorker` running as a `BackgroundService` with resilient retry loops) provided clean, decoupled lifecycle management.
