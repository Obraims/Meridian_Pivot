# Solstice Events Co. - Event Check-in Kiosk Architecture Overview

## 1. System Overview

The **Solstice Events Check-in Kiosk** is a high-reliability event attendee badge printing and check-in system designed for **Solstice Events Co.**. It manages attendee check-ins for events, ensuring that each badge is printed asynchronously, duplicate badge prints are strictly prevented, and webhook completions from the vendor are processed idempotently.

---

## 2. Pre-Pivot Architecture (Original Synchronous Design)

Under the original Day 3 design, the kiosk initiated check-in by calling the badge printer vendor synchronously over HTTP REST and waiting for the printer to finish before marking the attendee as `CHECKED_IN`.

```text
┌────────────────────────┐
│      Kiosk Scan        │
└───────────┬────────────┘
            │ 1. POST /checkin
            ▼
┌────────────────────────┐
│    Check-in Service    │
└───────────┬────────────┘
            │ 2. Synchronous HTTP POST /print (BLOCKED waiting for hardware)
            ▼
┌────────────────────────┐
│  Vendor Badge Printer  │
└───────────┬────────────┘
            │ 3. 200 OK (Print Complete)
            ▼
┌────────────────────────┐
│     SQLite DB State    │ -> CHECKED_IN
└────────────────────────┘
```

**Flaws of the Synchronous Architecture**:
- Kiosks were blocked waiting for physical printer hardware (500ms–3000ms latency per attendee).
- Vendor deprecation of synchronous REST printing made this architecture obsolete.
- Network timeouts between kiosk and printer created ambiguity around whether a badge was printed.

---

## 3. Post-Pivot Architecture (Asynchronous Event-Driven Messaging)

On Day 4, Solstice Events pivoted to an asynchronous, message-driven architecture using **RabbitMQ** for reliable print job queuing and **Webhooks** for printer completion callbacks.

```text
┌────────────────────────┐
│  Attendee / Kiosk UI   │ (Staff scans QR code: Alice, Brian, Carol)
└───────────┬────────────┘
            │ 1. POST /api/checkin/{attendeeId}
            ▼
┌────────────────────────┐
│    Check-in Service    │
│  (ASP.NET Core Minimal)│
└─────┬──────────────┬───┘
      │              │ 2. State -> PENDING (Immediate HTTP 200 returned)
      │              ▼
      │      ┌───────────────┐
      │      │ SQLite DB     │ (attendees, print_jobs)
      │      └───────────────┘
      │
      │ 3. Publish Message { printJobId, attendeeId, attendeeName }
      ▼
┌────────────────────────┐
│        RabbitMQ        │
│ (badge-print-requests) │
└───────────┬────────────┘
            │ 4. AMQP Consume
            ▼
┌────────────────────────┐
│   Mock Badge Printer   │
│   (BackgroundWorker)   │ -> Simulates physical hardware print duration
└───────────┬────────────┘
            │ 5. Callback: POST /api/webhooks/print-completed
            ▼
┌────────────────────────┐
│     Webhook Service    │
└───────────┬────────────┘
            │ 6. Persistent Idempotency Check (webhook_events)
            │ 7. State -> CHECKED_IN (if PENDING)
            ▼
┌────────────────────────┐
│      SQLite DB         │
└────────────────────────┘
```

---

## 4. Key Architectural Components & Responsibilities

1. **Check-in Service (`CheckinService.cs`)**:
   - Validates attendee existence (`A001`, `A002`, `A003`).
   - Enforces **Duplicate-Scan Protection**:
     - If attendee is `CHECKED_IN` $\rightarrow$ returns duplicate message without creating print jobs or publishing to RabbitMQ.
     - If attendee is `PENDING` $\rightarrow$ returns in-progress message without creating duplicate print jobs.
   - For `NOT_CHECKED_IN` attendees:
     - Updates attendee status in SQLite to `PENDING`.
     - Creates a `print_jobs` record with status `pending`.
     - Publishes a `BadgePrintMessage` to the RabbitMQ queue `badge-print-requests`.
     - Immediately returns HTTP `200 OK` with status `PENDING`.

2. **RabbitMQ Broker & Publisher (`BadgePrintPublisher.cs`)**:
   - Connects to RabbitMQ on `localhost:5672` using `RabbitMQ.Client`.
   - Declares the durable queue `badge-print-requests`.
   - Publishes persistent JSON messages containing `{ printJobId, attendeeId, attendeeName }`.

3. **Mock Badge Printer Worker (`MockBadgePrinterWorker.cs`)**:
   - Runs as an ASP.NET Core `BackgroundService`.
   - Consumes messages from the `badge-print-requests` queue.
   - Simulates physical badge printing (1000ms delay).
   - Generates a unique event ID (`evt-<guid>`) and invokes `POST /api/webhooks/print-completed`.
   - Acknowledges messages to RabbitMQ upon successful dispatch (`BasicAck`).

4. **Persistent Webhook Idempotency (`WebhookService.cs` & `webhook_events`)**:
   - Webhook callbacks check the SQLite `webhook_events` table (`event_id`, `attendee_id`, `status`, `received_at`).
   - Duplicate or replayed webhook events return a successful 200 OK with `isDuplicate = true`, preventing double state transitions.
   - Out-of-order or late webhooks are safely recorded without corrupting attendee states.

5. **Kiosk UI (`frontend/`)**:
   - Self-service dashboard showing real-time attendee status cards.
   - Displays clear state progression: `Ready to Check In` $\rightarrow$ `⏳ Printing...` $\rightarrow$ `✓ Checked In`.
   - Polls `/api/attendees` and `/api/print-jobs` to reflect asynchronous webhook completion dynamically.

---

## 5. State Machine Lifecycle

```
┌────────────────────┐
│   NOT_CHECKED_IN   │
└─────────┬──────────┘
          │ Scan QR (POST /api/checkin/{id})
          ▼
┌────────────────────┐
│      PENDING       │  <--- Duplicate scan ignored here
└─────────┬──────────┘
          │ Webhook received (POST /api/webhooks/print-completed)
          ▼
┌────────────────────┐
│     CHECKED_IN     │  <--- Duplicate scan ignored here
└────────────────────┘
```
