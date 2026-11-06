# Architecture Overview

## High-Level Design

The system follows **Clean Architecture** with a clear separation of concerns
across four .NET projects, a React SPA, and SQL Server.

```
 Browser (React SPA)
        │  HTTP/JSON (REST API)  +  WebSockets (SignalR)
        ▼
   FurnaceMonitor.Api  ─ Controllers, Middleware, JWT, Swagger, Program.cs
        │ DI
        ▼
   FurnaceMonitor.Application ─ DTOs, Interfaces, Services, Validation
        │ DI
        ▼
   FurnaceMonitor.Infrastructure ─ DbContext, Repositories, SignalR Hub, JWT, BCrypt, Seeding
        │ EF Core
        ▼
   FurnaceMonitor.Domain ─ Entities, Enums, Base Classes
        │
        ▼
   SQL Server
```

## Projects & Responsibilities

### FurnaceMonitor.Domain
Pure domain model with **no dependencies**:
- `Common/EntityBase` — Id, CreatedAtUtc/By, UpdatedAtUtc/By.
- Entities: `Role`, `User`, `Furnace`, `FurnaceBatch`, `FurnaceChemical`,
  `SignalRConnection`, `AuditLog`, `UserSession`.
- Enums: `UserRole`, `IndicatorColor`, `FurnaceStatus`, `BatchStatus`, `AuditAction`.

### FurnaceMonitor.Application
Orchestration and contracts (depends only on Domain):
- DTOs for each module (`Auth`, `Furnace`, `Batch`, `History`, `Dashboard`, `Admin`, `Realtime`).
- Interfaces: repositories, unit-of-work and service contracts.
- `AppException` for domain rule violations.
- `DependencyInjection.cs` for service registration (kept lightweight).

### FurnaceMonitor.Infrastructure
Implementation of Application contracts (depends on Application + Domain):
- `Persistence/AppDbContext` + fluent `Configurations`.
- `GenericRepository<T>` + `UnitOfWork` + `UnitOfWorkAccessor`.
- `Security/PasswordHasher` (BCrypt) and `Security/TokenService` (JWT).
- Services: `AuthService`, `FurnaceService`, `BatchService`, `HistoryService`,
  `DashboardService`, `AuditService`, `UserManagementService`,
  `SignalRConnectionService`, `RealtimeNotificationService`.
- `SignalR/FurnaceHub` — real-time push with per-furnace groups.
- `Persistence/InfrastructureSeed` — auto migration + seed.
- `Migrations/` — EF Core initial schema.

### FurnaceMonitor.Api
Composition root (Web API host):
- `Controllers/` — Auth, Furnace, Batch, Dashboard, History, Users.
- `Middleware/` — Exception handling + request logging.
- `Extensions/CurrentUserExtensions` — resolve current user id from the JWT.

## Business Rules

- **One active batch per furnace.** Attempting to start a second active batch is rejected.
- **Clear Furnace** soft-deactivates the active batch:
  - `IsActive = false`, `Status = Cleared`, `ClosedTimeUtc = now`, `EndedAtUtc = now`.
  - No records are deleted.
  - A `FurnaceClearedDto` is broadcast to the furnace group → displays show *No Active Batch*.
- **Close Batch** records a normal end: `Status = Closed`, `IsActive = false`.
- **Chemical validation** — Si/Ti/Mg are `decimal(18,4)`, limited to `MaxChemicalValue`.

## Real-Time Flow

```
Operator saves/updates/clears batch
        │
        ▼
BatchService ──► RealtimeNotificationService (INotificationService)
        │
        ├──► FurnaceHub.JoinGroup(FURNACE:{Number})
        │        └──► Clients.Group.ReceiveUpdate(dto) / ReceiveClear(dto)
        │
        └──► AuditService records action
```

- Client calls `JoinFurnace("F1")` to subscribe to group `FURNACE:F1`.
- Client calls `LeaveFurnace("F1")` on unmount.
- Auto-reconnect with exponential backoff; on reconnect the client re-joins groups.

## Security Model

- **Authentication**: JWT Bearer tokens.
- **Authorization**: `[Authorize(Roles = "Admin,Operator")]` on controllers/actions.
- **Passwords**: BCrypt hashing (never plain text).
- **Sessions**: `UserSessions` + refresh tokens for token rotation.
- **Audit**: every meaningful action is logged to `AuditLogs`.
- **XSS**: React escapes output by default.
- **SQL Injection**: EF Core parameterized queries.

## High Availability / Scale

- SignalR scales to many groups; each furnace is isolated to its own group.
- Reads are lightweight; dashboard counts are aggregated on demand.
- Designed to support 20+ furnaces, 100+ operators, 100+ displays concurrently.

## Folder Reference

See `README.md` for the full tree.

