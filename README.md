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

## Deployment & Running Options

### Option 1: Local Development / Evaluation (Recommended for Live Demo)

#### Step 1: Start RabbitMQ Broker
```powershell
docker run -d --name meridian-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

#### Step 2: Run Automated Tests
```powershell
cd tests
dotnet test
```

#### Step 3: Launch ASP.NET Core Application
```powershell
cd backend
dotnet run --urls="http://127.0.0.1:5000"
```

Open your browser at `http://127.0.0.1:5000/` to access the live Kiosk UI!

---

### Option 2: Single-Command Containerized Deployment (`docker compose`)

To run the entire system (RabbitMQ + Backend + SQLite + Frontend) in isolated Docker containers:

```powershell
docker compose up --build
```
- **Kiosk UI**: `http://localhost:5000/`
- **RabbitMQ Management Portal**: `http://localhost:15672/` (Username: `guest`, Password: `guest`)

To stop and remove containers:
```powershell
docker compose down
```

---

### Option 3: Standalone Binary Release (`dotnet publish`)

To package a standalone executable for deployment without source files:
```powershell
dotnet publish backend/StockSync.csproj -c Release -o ./publish
```
Then run the generated `publish/StockSync.exe` (Windows) or `publish/StockSync` (Linux/macOS).
