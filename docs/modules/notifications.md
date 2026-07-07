# Notifications Module

The `Notifications` module is an optional durable history/read-state module for user-facing notifications. It complements the shared SSE/SignalR live-delivery adapters; it is not a backend event bus and it is not a replacement for module integration events.

## Projects

```text
Gma.Modules.Notifications.Contracts
Gma.Modules.Notifications.Domain
Gma.Modules.Notifications.Application
Gma.Modules.Notifications.Persistence
Gma.Modules.Notifications.Persistence.SqlServerMigrations
Gma.Modules.Notifications.Persistence.PostgreSqlMigrations
Gma.Modules.Notifications.Api
Gma.Modules.Notifications.Admin.Contracts
Gma.Modules.Notifications.AdminApi
```

The module is not registered in the default hosts. Applications compose the user API and admin API explicitly when they need notification history.

`NotificationsProfiles.Default` is selected by both `Gma.Modules.Notifications.Api` and `Gma.Modules.Notifications.AdminApi`. It provides the `notifications.history` and `notifications.broadcasts` composition features and requires tenant context. Live SSE/SignalR delivery is still host-selected through the shared notification adapters, not implied by the durable module profile.

## User API

`Gma.Modules.Notifications.Api` maps current-user endpoints:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/notifications` | List the current user's notification history. |
| `GET` | `/api/notifications/{notificationId}` | Get one current-user notification. |
| `POST` | `/api/notifications/{notificationId}/read` | Mark one current-user notification as read. |
| `POST` | `/api/notifications/read-all` | Mark all current-user notifications as read. |
| `GET` | `/api/notifications/history/stream` | Stream newly committed history rows for the current user. |
| `GET` | `/api/notifications/broadcasts` | List tenant/platform broadcasts visible to the current user. |
| `GET` | `/api/notifications/broadcasts/{broadcastId}` | Get one visible broadcast. |
| `POST` | `/api/notifications/broadcasts/{broadcastId}/read` | Mark one visible broadcast as read for the current user. |
| `POST` | `/api/notifications/broadcasts/read-all` | Mark all visible broadcasts as read for the current user. |
| `GET` | `/api/notifications/broadcasts/stream` | Stream newly committed user-targeted broadcasts. |

The stream accepts optional `afterSequence`. When omitted, the stream starts after the user's current maximum durable sequence, so it behaves as a live stream. When supplied, it replays rows with a greater `StreamSequence`, which gives clients a reconnect cursor.

All endpoints require authentication and tenant context. When tenancy is enabled, the tenant claim on the token must match the active tenant context.

Current-user history endpoints use the shared access-subject foundation. The API constructs an explicit `AccessSubject` from the authenticated user and active tenant, while `Gma.Modules.Notifications.Application.Visibility.NotificationHistoryAccess` owns the simple user/tenant checks. Single-notification reads and mark-read operations check a minimal access summary and return not-found-shaped results for wrong-user or wrong-tenant access. List, stream, cursor, and read-all paths keep visibility constrained inside repository queries.

## Admin API

`Gma.Modules.Notifications.AdminApi` maps admin-only endpoints under `/api/admin/notifications`:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/admin/notifications` | List tenant notification history, optionally filtered by user and unread state. |
| `GET` | `/api/admin/notifications/{notificationId}` | Get one tenant notification. |
| `GET` | `/api/admin/notifications/history/stream` | Stream committed tenant history rows, optionally filtered by user. |
| `GET` | `/api/admin/notifications/broadcasts` | List tenant-scoped broadcasts. |
| `POST` | `/api/admin/notifications/broadcasts` | Create a tenant-scoped broadcast. |
| `GET` | `/api/admin/notifications/platform-broadcasts` | List platform-scoped broadcasts. |
| `POST` | `/api/admin/notifications/platform-broadcasts` | Create a platform-scoped broadcast. |
| `GET` | `/api/admin/notifications/broadcasts/inbox` | List broadcasts visible to the current admin actor. |
| `POST` | `/api/admin/notifications/broadcasts/inbox/{broadcastId}/read` | Mark one admin-targeted broadcast as read. |
| `POST` | `/api/admin/notifications/broadcasts/inbox/read-all` | Mark all visible admin broadcasts as read. |
| `GET` | `/api/admin/notifications/broadcasts/inbox/stream` | Stream newly committed admin-targeted broadcasts. |

Admin endpoints use the shared admin API executor, audit pipeline, tenant requirement where applicable, and Notifications permissions:

- `notifications.history.read`
- `notifications.broadcasts.read`
- `notifications.broadcasts.create`

Admins use a separate API surface from normal users because tenant-wide history and broadcast management are operational capabilities, not self-service user capabilities.

## Persistence

The module owns the `notifications` schema with:

- `user_notifications` for history/read state;
- `notification_broadcasts` for tenant/platform broadcasts;
- `notification_broadcast_reads` for per-recipient broadcast read receipts;
- `inbox_messages` for idempotent integration-event processing.

Stored notification fields include tenant id, user id, source module, notification name/version, title/body/severity, occurrence time, stored time, read timestamp, canonical payload JSON, and a database-generated `StreamSequence`.

`StreamSequence` is the durable stream cursor. It avoids timestamp-only polling gaps and is indexed by tenant/user for current-user streams and by tenant for admin streams.

Broadcasts have their own `StreamSequence`. Do not reuse a user history cursor for broadcast streams or vice versa. Tenant broadcasts carry a tenant id; platform broadcasts leave tenant id null and are included explicitly by the broadcast repository.

Durable history and broadcast streams are configured through `Notifications:DurableStreams`. Defaults are a `25` item batch and a `00:00:01` poll interval. The module validates this at startup so oversized batches or tight poll loops fail fast. Poll-time query failures are logged and close the stream; clients should reconnect with their last acknowledged sequence.

SQL Server and PostgreSQL migrations are provider-specific and use the schema-local EF history table.

## Durable Ingestion

`Gma.Modules.Notifications.Contracts` owns `UserNotificationRequestedIntegrationEvent`. Producer modules that want guaranteed history creation publish that event through their own outbox and declare it in their descriptor. The physical subject remains producer-scoped:

```text
{application-namespace}.{producer-module}.user-notification-requested.v1
```

The compiled Ordering example publishes affected-order-owner notification requests:

```text
gma.ordering.user-notification-requested.v1
```

`Gma.Modules.Notifications.Application` exposes `AddUserNotificationRequestSubscription(producerModule)` for hosts or examples that want this module to consume a producer's durable notification requests. The NATS consumer loop writes the notification and inbox processed marker in the `notifications` schema transaction, giving at-least-once delivery with module-owned idempotency.

This pattern is explicit by design. The reusable Notifications descriptor does not subscribe to Catalog or any other producer by default. If a producer should feed notification history, compose a producer-specific subscription in the host/example runtime. Do not add cross-module EF links or direct writes into `Gma.Modules.Notifications.Persistence`.

## Shared Live Adapters

The older shared path still exists for best-effort live delivery:

- transactional commands may enqueue through `IUserNotificationRequestQueue`;
- the optional CQRS bridge flushes after the source module unit of work commits;
- `IUserNotificationPublisher` can store history through `IUserNotificationHistoryWriter` when the module is composed;
- live SSE/SignalR sinks deliver only when `Notifications:Enabled=true`.

Use the durable integration-event path for notifications that must survive process crashes between source commit and live publish.

## Composition

Reference and register only the surfaces needed by the host:

```csharp
using Gma.Modules.Notifications.Api;
using Gma.Modules.Notifications.AdminApi;

builder.AddModule<NotificationsModule>();
builder.AddAdminApiModule<NotificationsAdminApiModule>();
```

Run the provider-specific module migrations before starting the host. To consume NATS notification request events, the runtime host must also compose NATS consumers and the producer-specific subscription:

```csharp
builder.Services.AddNotificationsApplication();
builder.Services.AddUserNotificationRequestSubscription(OrderingModuleMetadata.Name);
```

Do this only in hosts/examples that intentionally compose both the producing module and the Notifications consumer.

Payload JSON is normalized and bounded to 32 KB for both direct history rows and broadcasts. Broadcast read receipts are idempotent per recipient scope/kind/id and use provider-specific insert-if-missing behavior so retry/concurrent read calls stay safe. The recipient scope includes the current/default tenant context when present, which prevents platform broadcast read state from crossing tenants with reused opaque recipient ids. For non-tenant projects, omit `TenancyModule`; the local default tenant id remains the broadcast tenant scope.

## Boundaries

- Other modules may reference `Gma.Modules.Notifications.Contracts`.
- Other modules must not reference `Gma.Modules.Notifications.Application`, `Gma.Modules.Notifications.Domain`, `Gma.Modules.Notifications.Persistence`, `Gma.Modules.Notifications.Api`, or `Gma.Modules.Notifications.AdminApi`.
- Producing modules publish notification requests through their own outbox; they never write notification history directly.
- Durable business decisions must use integration events and local projections, not notification history.

## Follow-Ups

- Add user notification preferences and retention policies.
- Add retention cleanup/admin operations.
- Consider wildcard notification request subscriptions only as a shared messaging feature, not as hidden Notifications module magic.
