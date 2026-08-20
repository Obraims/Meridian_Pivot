# Solstice Events Co. - Event Check-in Kiosk

A lightweight, reliable attendee check-in and asynchronous badge printing service built with **C# / ASP.NET Core Minimal API (.NET 10)**, **SQLite (ADO.NET)**, and **RabbitMQ**.

---

## Architecture Flow

```
Kiosk UI (Scan QR)
    │
    ▼
POST /api/checkin/{attendeeId}  ──(State: PENDING)──> SQLite DB
    │
    ▼
RabbitMQ (badge-print-requests)
    │
    ▼
Mock Badge Printer Worker (BackgroundService)
    │
    ▼
POST /api/webhooks/print-completed
    │
    ▼
Webhook Idempotency (SQLite webhook_events) ──(State: CHECKED_IN)──> SQLite DB
```

---

## Key Features

1. **Asynchronous Badge Printing**: Check-in requests are queued to RabbitMQ immediately without blocking kiosk staff.
2. **Duplicate-Scan Protection**: Attendees who are already `CHECKED_IN` or `PENDING` cannot generate duplicate print jobs or queue messages.
3. **Persistent Webhook Idempotency**: Webhook events are recorded in SQLite (`webhook_events`) to safely ignore duplicate callbacks.
4. **Self-Service Kiosk UI**: Plain HTML/CSS/JavaScript interface displaying real-time attendee states (`Ready to Check In` $\rightarrow$ `⏳ Printing...` $\rightarrow$ `✓ Checked In`).

---

## Running the Application

### 1. Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [RabbitMQ Broker](https://www.rabbitmq.com/) running on `localhost:5672` (e.g. via Docker: `docker run -d --name meridian-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`)

### 2. Run Tests
```powershell
cd tests
dotnet test
```

### 3. Run Application
```powershell
cd backend
dotnet run --urls="http://127.0.0.1:5000"
```

Open your browser at `http://127.0.0.1:5000/` to use the Kiosk UI!
