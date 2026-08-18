# StockSync

StockSync is an inventory synchronization engine that reconciles and synchronizes inventory records from a source system to a destination database using SQLite persistence.

## Architecture

- **Runtime**: .NET 10.0 (ASP.NET Core Minimal API)
- **Database**: SQLite (`database/stocksync.db`) via `Microsoft.Data.Sqlite` (ADO.NET)
- **Data Access**: Repository Pattern with parameterized raw SQL and transaction safety
- **Testing**: xUnit test suite (`tests/StockSync.Tests.csproj`)

---

## Phase 3 — Inventory Synchronization Engine

### Features
1. **Source & Destination Comparison**:
   - **Case A (New Product)**: Products existing in source but absent in destination are inserted into destination.
   - **Case B (Quantity Update)**: Products in both stores with mismatched quantities are updated in destination.
   - **Case C (Product Removed)**: Products absent from source but present in destination are removed from destination.
2. **Transaction Safety**:
   - All synchronization changes (additions, updates, deletions) execute atomically inside a single SQLite transaction.
3. **Audit & History Tracking**:
   - Each synchronization run records an entry in `sync_history` with timestamps, status (`Success` / `Failed`), and detailed counts.

---

## API Endpoints

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/health` | Health and status check |
| `GET` | `/source/inventory` | List all source inventory items |
| `GET` | `/destination/inventory` | List all destination inventory items |
| `POST` | `/api/sync` | Trigger inventory synchronization |
| `GET` | `/sync/history` | View history of synchronization runs |

### Synchronization Endpoint Response
```json
{
  "success": true,
  "startedAt": "2026-08-20T05:27:24.1036238Z",
  "completedAt": "2026-08-20T05:27:24.136571Z",
  "itemsChecked": 3,
  "itemsAdded": 3,
  "itemsUpdated": 0,
  "itemsRemoved": 0,
  "changesApplied": 3,
  "errorMessage": null
}
```

---

## Running the Project

### Start Backend
```bash
cd backend
dotnet run --urls="http://localhost:5000"
```

### Run Tests
```bash
dotnet test tests/StockSync.Tests.csproj
```