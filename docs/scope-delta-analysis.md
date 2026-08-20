# Solstice Events Co. - Scope Delta Analysis (Meridian Pivot)

## 1. Executive Summary

This document details the architectural and functional delta between the original synchronous badge-printer integration (Day 3) and the asynchronous message-driven architecture (Day 4 Pivot) for Solstice Events Co., satisfying the requirements for **Assignment 2: Mid-Sprint Change Log & Refactored Deliverable**.

---

## 2. Detailed Scope Delta (Dropped / Modified / Added / Preserved)

### DROPPED
- **Synchronous Vendor REST Calls**: The kiosk backend no longer blocks or waits for the badge printer vendor HTTP response during the initial check-in scan.
- **Immediate State Transition to CHECKED_IN**: Check-in no longer immediately switches to `CHECKED_IN` upon HTTP request return.
- **Obsolete Inventory Domain Logic**: Retail inventory models, warehouse sync tables, and polling schedulers from prior project phases were completely retired.

### MODIFIED
- **Check-in Workflow**: The `POST /api/checkin/{attendeeId}` endpoint now returns `PENDING` immediately after queuing the print request.
- **Attendee State Model**: Enhanced to support three distinct states (`NOT_CHECKED_IN`, `PENDING`, `CHECKED_IN`).
- **Printer Integration**: Communication shifted from synchronous HTTP client calls to asynchronous AMQP messaging via RabbitMQ.
- **Kiosk UI**: The UI was updated to show the `Printing...` intermediate state and poll for webhook completion instead of assuming immediate confirmation.

### ADDED
- **RabbitMQ Integration (`RabbitMQ.Client 7.2.2`)**: Real AMQP broker queue `badge-print-requests` on `localhost:5672`.
- **Badge Print Publisher (`BadgePrintPublisher.cs`)**: Dedicated service to publish print requests to RabbitMQ.
- **Mock Badge Printer Worker (`MockBadgePrinterWorker.cs`)**: Background worker simulating printer hardware delays and dispatching webhook callbacks.
- **Webhook Endpoint (`POST /api/webhooks/print-completed`)**: Ingestion point for asynchronous printer completion events.
- **Persistent Webhook Idempotency**: `webhook_events` table in SQLite ensuring that replayed or duplicate webhook callbacks never trigger duplicate state changes or extra badge prints.
- **Reset Endpoint (`POST /api/reset`)**: Administrative endpoint to reset attendee states for demonstration purposes.

### PRESERVED
- **Attendee Identification**: Attendees `A001` (Alice), `A002` (Brian), and `A003` (Carol) are uniquely identified and persisted.
- **Duplicate-Scan Protection**: Crucial business rule preventing multiple badge prints for the same attendee remains 100% enforced.
- **Simple SQLite Persistence (ADO.NET)**: Clean ADO.NET data access patterns without heavy ORMs.
- **Kiosk Usability**: Simple, responsive HTML/CSS/JavaScript interface.

---

## 3. Reprioritized Day 4–5 Backlog

| Backlog Item | Priority | Status | Rationale |
| :--- | :---: | :---: | :--- |
| **RabbitMQ Queue Infrastructure** | P0 | **DONE** | Required to replace deprecated synchronous HTTP calls with persistent queueing. |
| **3-State Machine Transition (`NOT_CHECKED_IN` $\rightarrow$ `PENDING` $\rightarrow$ `CHECKED_IN`)** | P0 | **DONE** | Core domain requirement for asynchronous decoupled lifecycle. |
| **Mock Printer Background Worker** | P0 | **DONE** | Simulates hardware print delay and emits webhook callback. |
| **Webhook Endpoint & SQLite Idempotency** | P0 | **DONE** | Ensures duplicate or out-of-order webhooks cannot corrupt state or trigger duplicate prints. |
| **Duplicate-Scan Regression Verification** | P0 | **DONE** | Verified both `PENDING` and `CHECKED_IN` duplicate scans are blocked. |
| **Kiosk UI Real-Time Feedback** | P1 | **DONE** | Live UI reflecting `Ready to Check In` $\rightarrow$ `⏳ Printing...` $\rightarrow$ `✓ Checked In`. |
| **Comprehensive Test Suite** | P1 | **DONE** | 9 unit and integration tests covering all state transitions and RabbitMQ integration. |

---

## 4. Regression Verification: Duplicate-Scan Protection

| Scenario | Pre-Pivot Behavior | Post-Pivot Behavior | Status |
| :--- | :--- | :--- | :---: |
| Scan un-scanned attendee (`A001`) | Synchronous print -> `CHECKED_IN` | Queue RabbitMQ -> `PENDING` -> Webhook -> `CHECKED_IN` | **VERIFIED** |
| Rescan already `CHECKED_IN` attendee | Blocked with duplicate message | Blocked with duplicate message; no RabbitMQ publish | **VERIFIED** |
| Rescan attendee while `PENDING` | N/A (was synchronous) | Blocked with in-progress message; no second print job | **VERIFIED** |
| Replay duplicate webhook event | N/A | Ignored via SQLite `webhook_events` deduplication | **VERIFIED** |

---

## 5. Architectural Trade-Off Analysis

1. **Decoupled Throughput vs Eventual Consistency**:
   - *Gain*: Kiosks respond in $< 20\text{ms}$ rather than waiting seconds for printer hardware, dramatically increasing check-in line throughput.
   - *Trade-off*: Introduces a transient `PENDING` state that requires UI polling / real-time updates.

2. **Broker Dependency**:
   - *Gain*: Messages in RabbitMQ survive worker restarts and network partitions.
   - *Trade-off*: Requires RabbitMQ infrastructure (`localhost:5672`), which was successfully integrated.
