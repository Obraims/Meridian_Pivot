# Solstice Events Co. - Event Check-in Kiosk

A reliable attendee check-in and asynchronous badge printing service built with **C# / ASP.NET Core Minimal API (.NET 10)**, **SQLite (ADO.NET)**, and **RabbitMQ**.

---

## 1. Project Context & The Pivot

This project implements the **Solstice Events Co.** event check-in kiosk system adapted for the **Meridian Pivot**:

- **Pre-Pivot (Day 3)**: Kiosk performed a synchronous REST call to the badge printer vendor, blocking until the hardware responded before marking the attendee as `CHECKED_IN`.
- **Post-Pivot (Day 4/5)**: The vendor deprecated the synchronous API. The system now publishes a print request to a **RabbitMQ** queue (`badge-print-requests`), immediately sets attendee state to `PENDING`, and confirms `CHECKED_IN` only after receiving an asynchronous webhook callback (`POST /api/webhooks/print-completed`).

---

## 2. Architecture Flow

```
Kiosk UI (Scan QR)
    │
    ▼
POST /api/checkin/{attendeeId}  ──(State: PENDING)──> SQLite DB (stocksync.db)
    │
    ▼
RabbitMQ Queue (badge-print-requests on localhost:5672)
    │
    ▼
Mock Badge Printer Worker (BackgroundService, 1s hardware delay)
    │
    ▼
POST /api/webhooks/print-completed
    │
    ▼
Persistent Idempotency (SQLite webhook_events) ──(State: CHECKED_IN)──> SQLite DB
```

---

## 3. Key Functionality & Integrity Rules

1. **At Least 3 Attendees Supported**: `A001` (Alice), `A002` (Brian), `A003` (Carol) seeded in SQLite.
2. **Asynchronous Processing**: Scans return `200 OK` with `PENDING` status in $< 20\text{ms}$; hardware print delay is decoupled via RabbitMQ.
3. **Duplicate-Scan Protection**: Scanning an attendee who is already `PENDING` or `CHECKED_IN` is rejected without creating additional print jobs or queue messages.
4. **Persistent Webhook Idempotency**: Webhook events are logged to the `webhook_events` table; replayed or duplicate webhook callbacks are safely dropped.
5. **No Fake Queue Abstraction**: Genuine AMQP communication via `RabbitMQ.Client 7.2.2`.

---

## 4. How to Run the Project (Evaluation Guide)

### Prerequisites
- **.NET 10 SDK** (`dotnet --version` >= `10.0.x`)
- **Docker Desktop** (for running RabbitMQ)

---

### Step 1: Start the RabbitMQ Broker

If running for the first time:
```powershell
docker run -d --name meridian-rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

If the container already exists from a previous run:
```powershell
docker start meridian-rabbitmq
```

---

### Step 2: Run Automated Tests
```powershell
dotnet test tests/StockSync.Tests.csproj
```
*(Runs 9 focused unit and integration tests covering attendee scan, RabbitMQ publishing, webhook handling, duplicate scan prevention, and idempotency).*

---

### Step 3: Start the Backend & Kiosk UI
```powershell
dotnet run --project backend/StockSync.csproj --urls="http://127.0.0.1:5000"
```

Open your browser at:
👉 **`http://127.0.0.1:5000/`** (or `http://localhost:5000/`)

---

### 5. Live Demonstration Guide

1. **Initial State**: All 3 attendees (`A001 Alice`, `A002 Brian`, `A003 Carol`) display `Ready to Check In`.
2. **First Scan**: Click **"Scan Badge"** on Alice. The UI immediately displays `⏳ Printing Badge... (PENDING)` while publishing to RabbitMQ.
3. **Completion**: In ~1 second, the background worker completes simulated printing, dispatches the webhook callback, and Alice transitions to `✓ Checked In`.
4. **Duplicate Scan**: Click **"Re-scan Badge"** on Alice. The system displays the `ALREADY CHECKED IN` warning with `NO NEW BADGE WAS PRINTED` and creates zero additional jobs.
5. **Audit**: The audit table displays exactly one completed print job per attendee.
