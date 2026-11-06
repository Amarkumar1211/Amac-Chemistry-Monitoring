# API Reference

Base URL (development): `https://localhost:7111/api`

All endpoints (except login/refresh) require an `Authorization: Bearer <token>` header.
Responses use the envelope shape:

```json
{
  "success": true,
  "data": { },
  "message": "optional",
  "errors": { }
}
```

---

## Authentication

### POST `/api/auth/login`
Authenticate and obtain tokens.

```json
{ "username": "admin", "password": "Admin@123" }
```
Returns `AuthResponse`: `token`, `refreshToken`, `userId`, `username`,
`displayName`, `role`, `expiresAtUtc`.

### POST `/api/auth/refresh`
Rotate a refresh token.

```json
{ "refreshToken": "<refresh-token>" }
```

### GET `/api/auth/me`
Return the current authenticated user (from the JWT).

### POST `/api/auth/logout`
Invalidate the current session / refresh token.

---

## Furnace

### GET `/api/furnace`
List all furnaces.

### GET `/api/furnace/{id}`
Get a single furnace.

### POST `/api/furnace`
Create/update a furnace. Body: `{ number, name, status, isActive, location, sortOrder }`.

### DELETE `/api/furnace/{id}`
Deactivate a furnace (soft delete; records preserved).

---

## Batch

### GET `/api/batch/active/{furnaceId}`
Return the active batch for a furnace, or `null` if none.

### GET `/api/batch/{id}`
Get a batch by id (with current chemicals).

### POST `/api/batch`
Start a new batch.
```json
{ "batchNumber": "B-2025-001", "furnaceId": 1, "indicator": 1, "instruction": "", "remarks": "" }
```
Rejects if the furnace already has an active batch.

### PUT `/api/batch/{id}`
Update chemicals/indicator/instruction.
```json
{ "si": 1.2, "ti": 0.4, "mg": 0.9, "indicator": 2, "instruction": "reduce", "remarks": "" }
```
Broadcasts `ReceiveUpdate` to the furnace group.

### POST `/api/batch/clear/{furnaceId}`
```json
{ "confirm": true }
```
Soft-clears the active batch; broadcasts `ReceiveClear`.

### POST `/api/batch/close/{id}`
```json
{ "confirm": true }
```
Closes the running batch normally.

---

## Dashboard

### GET `/api/dashboard`
Returns: `totalFurnaces`, `activeBatches`, `connectedDisplays`,
`onlineOperators`, `greenCount`, `yellowCount`, `redCount`, `inactiveCount`.

---

## History

### GET `/api/history/batches?furnaceId=&status=&from=&to=`
List batch history with optional filters.

---

## Users (Admin)

### GET `/api/users`
List users.

### POST `/api/users`
Create a user. Body: `{ username, displayName, email, password, role }`.

### PUT `/api/users/{id}`
Update a user (role/display/email/status).

### DELETE `/api/users/{id}`
Deactivate a user.

### POST `/api/users/{id}/password`
Admin reset of a user password. Body: `{ adminPassword, newPassword }`.

---

## SignalR Hub — `/hubs/furnace`

Client methods:

| Direction | Method              | Payload         |
|-----------|---------------------|-----------------|
| → server  | `JoinFurnace`       | `string furnaceNumber` |
| → server  | `LeaveFurnace`      | `string furnaceNumber` |
| ← client  | `ReceiveUpdate`     | `FurnaceUpdateDto` |
| ← client  | `ReceiveClear`      | `FurnaceClearedDto` |
| ← client  | `ReceiveError`      | `string`        |

The hub authenticates via `accessTokenFactory`. Group names use the
`FURNACE:{Number}` convention.

