# Ordering Example Module

Ordering is a compiled optional example module. It is not registered in `Host.Api`, `Host.AdminCli`, or `Host.AdminApi` by default.

## Purpose

Ordering demonstrates consuming another module's integration events without referencing that module's internals.

Ordering owns:

- orders;
- local catalog item projections used for order decisions;
- an inbox table for idempotent event processing.
- projection rebuild checkpoints for local projection repair/backfill.
- an outbox for Ordering-owned integration events, including addressed notification requests for affected order owners.

## Projects

```text
Ordering.Contracts
Ordering.Domain
Ordering.Application
Ordering.Persistence
Ordering.Persistence.SqlServerMigrations
Ordering.Persistence.PostgreSqlMigrations
Ordering.Api
```

## Catalog Dependency

Ordering references `Catalog.Contracts` only.

It consumes:

- `CatalogItemCreatedIntegrationEvent`
- `CatalogItemUpdatedIntegrationEvent`
- `CatalogItemDiscontinuedIntegrationEvent`

It does not reference Catalog domain, application, persistence, API, or admin projects.

## Projection

`CatalogItemProjection` stores duplicated local data:

- catalog item id;
- SKU;
- name;
- price;
- currency;
- status.
- available region codes. An empty list means globally available.

`Order` stores the catalog item id as a scalar external id. There is no database foreign key to Catalog. It also stores the placing user id and requested region, because order visibility and item-availability decisions are Ordering-owned business rules.

Current-user order reads follow the scoped resource-access pattern:

```text
API subject
  -> OrderViewer
  -> OrderingVisibilityPolicy
  -> UserOrdersScope
  -> IOrderReadRepository.ListAsync(scope, page, ct)
```

The protected repository methods require `UserOrdersScope`, and `Ordering.Persistence.QueryScopes.ApplyUserOrdersScope(...)` applies tenant and user filters in SQL. This keeps user-owned order history from becoming a handler-only boolean check.

## Projection Rebuild Task

Ordering declares and registers a scope-aware task:

```text
ordering.rebuild-catalog-item-projections
```

The task uses worker group `projection-workers`, payload version `1`, and supports control messages through the task runtime. Its payload includes projection version, batch size, dry-run, and an optional cursor override.

The rebuild flow is:

```text
CatalogItemProjectionExportSource
  -> TaskProjectionRebuildRunner<CatalogItemProjectionExport>
  -> ProjectionRebuildRunner<CatalogItemProjectionExport>
  -> CatalogItemProjectionRebuildWriter
  -> ordering.catalog_item_projections
  -> ordering.projection_rebuild_checkpoints
```

Retrying the same task run resumes from `ordering.projection_rebuild_checkpoints`. A cursor override starts from the supplied cursor instead of the saved checkpoint. The writer is idempotent because it upserts by the local projection repository's stable `(ScopeId, CatalogItemId)` shape.

To run the example end to end, explicitly compose Catalog persistence, Ordering application/persistence, TaskRuntime application/persistence, and the task worker runtime with:

```text
Tasks:Worker:WorkerGroups:0 = projection-workers
```

Default hosts do not register Catalog, Ordering, or the projection worker group.

## Order Rules

Creating an order requires a known active catalog item projection. Unknown or discontinued catalog items are rejected.
Ordering validates duplicated projection data before creating an order: SKU and name must fit local persistence limits, currency must be a three-letter code, and price must be positive and fit mapped decimal precision without rounding. Order totals must also fit mapped decimal precision. This keeps copied data explicit and prevents a bad producer event or manual projection repair from becoming a late EF failure.

The public API requires a user subject and a region code. The application rejects order placement when the local Catalog projection is not available in that region. Order list/detail queries build a domain `UserOrdersScope` and keep repository queries scoped to the order owner.

## Public API

`Ordering.Api` maps scope-aware authenticated endpoints under `/api/orders`:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/orders` | List current-user orders. |
| `GET` | `/api/orders/{orderId}` | Get one current-user order. |
| `POST` | `/api/orders` | Place an order for a projected Catalog item and region. |

The API is optional and not registered by default. It composes only Ordering application and persistence; it does not call Catalog synchronously.

## Notifications

Ordering demonstrates affected-recipient notification decisions without making Notifications understand order rules:

```text
Catalog item updated/discontinued
  -> Ordering NATS consumer
  -> Ordering catalog projection update
  -> Ordering order-owner lookup
  -> Ordering outbox UserNotificationRequestedIntegrationEvent
  -> optional Notifications consumer
  -> notifications.user_notifications
```

Ordering references `Gma.Modules.Notifications.Contracts` only. It publishes `UserNotificationRequestedIntegrationEvent` under Ordering's producer subject:

```text
{application-namespace}.ordering.user-notification-requested.v1
```

The Notifications module stays deliberately dumb about Catalog and Ordering visibility. It stores and streams a notification only after a producer module has already chosen an explicit recipient user id.

## Consumers

Ordering registers handlers with:

```csharp
[IntegrationEventHandler(OrderingModuleMetadata.CatalogItemCreatedProjectionHandlerName)]
internal sealed class CatalogItemCreatedProjectionHandler
    : IIntegrationEventHandler<CatalogItemCreatedIntegrationEvent>;

builder.Services.AddIntegrationEventHandler<CatalogItemCreatedIntegrationEvent, CatalogItemCreatedProjectionHandler>(
    OrderingModuleMetadata.Name,
    CatalogModuleMetadata.Name);
```

Catalog event contracts carry `IntegrationEventNameAttribute`, `IntegrationEventVersionAttribute`, and `[ScopeAware]`, so Ordering metadata uses `WithSubscription<CatalogItemCreatedIntegrationEvent>(CatalogModuleMetadata.Name, ...)` while the application registration passes explicit consumer and producer module names. The NATS runtime invokes the handler. `OrderingInboxStore` records processing in the `ordering` schema.
