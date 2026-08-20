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

## 2. Resources Consulted

1. **Official RabbitMQ .NET Client Tutorial & API Reference** (v7.x Async Patterns).
2. **Microsoft Learn: Background tasks with hosted services in ASP.NET Core** (`BackgroundService` lifecycle).
3. **Microsoft.Data.Sqlite Documentation** (ADO.NET SQLite connection pooling & transactional execution).

---

## 3. Actual Setup Problems, Dead Ends & Blocker Resolutions

1. **RabbitMQ Broker Accessibility**:
   - *Problem / Dead End*: Initial connection to `localhost:5672` failed with TCP connection refused because Docker Desktop Linux engine had not finished starting.
   - *Fix / Resolution*: Started Docker Desktop and confirmed the `meridian-rabbitmq` container was healthy with ports `5672` (AMQP) and `15672` (Management UI) bound. Subsequent PowerShell TCP connection test passed.
2. **RabbitMQ.Client 7.x Asynchronous API Migration**:
   - *Problem / Dead End*: Legacy examples use synchronous `channel.QueueDeclare()` and `channel.BasicPublish()`, which do not exist in `RabbitMQ.Client` 7.x.
   - *Fix / Resolution*: Implemented modern task-based async methods (`QueueDeclareAsync`, `BasicPublishAsync`, `BasicAckAsync`) and `AsyncEventingBasicConsumer` with `ReceivedAsync` delegates.
3. **SQLite Table Schema Discrepancy during Pivot**:
   - *Problem / Dead End*: The pre-pivot `stocksync.db` database on disk had an older schema for `webhook_events` lacking the `attendee_id` column.
   - *Fix / Resolution*: Implemented dynamic SQLite PRAGMA table inspection (`EnsureColumnExists`) in `DatabaseInitializer.cs` to execute automatic schema migrations on startup.
4. **PowerShell Non-Interactive WebCmdlet Hang**:
   - *Problem / Dead End*: Background test scripts hung indefinitely at `Invoke-WebRequest http://127.0.0.1:5000/` because PowerShell 5.1 attempts to instantiate Internet Explorer COM objects without `-UseBasicParsing`.
   - *Fix / Resolution*: Added clean process termination and switched web requests to direct REST calls and `-UseBasicParsing`.

---

## 4. Time-Box vs. Actual Time Breakdown

| Phase | Time-Box Allocated | Actual Time Spent | Status |
| :--- | :---: | :---: | :---: |
| **Solo Recon & RabbitMQ Setup** | 1.0 hr | 0.5 hr | Complete |
| **Domain Modeling & SQLite Schema** | 0.5 hr | 0.25 hr | Complete |
| **Producer & Background Worker Consumer** | 1.0 hr | 0.5 hr | Complete |
| **Webhook Idempotency & Kiosk UI** | 0.5 hr | 0.25 hr | Complete |
| **Total Sprint Investment** | **3.0 hrs** | **1.5 hrs** | **Efficient & Complete** |
