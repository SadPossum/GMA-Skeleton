# Catalog Example Module

Catalog is a compiled optional example module. It is not registered in `Host.Api`, `Host.AdminCli`, or `Host.AdminApi` by default.

## Purpose

Catalog demonstrates stored tenant-scoped domain data, CQRS commands and queries, provider-split EF persistence, explicit cache-aside reads, admin CLI/API front doors, regional availability rules, and integration events through outbox.

## Projects

```text
Catalog.Contracts
Catalog.Domain
Catalog.Application
Catalog.Persistence
Catalog.Persistence.SqlServerMigrations
Catalog.Persistence.PostgreSqlMigrations
Catalog.Api
Catalog.Admin.Contracts
Catalog.AdminCli
Catalog.AdminApi
```

`Catalog.Contracts` follows the standard contract folders: `Api/` for item DTO/list contracts, `Events/` for item integration events and subjects, `Exports/` for projection rebuild/export contracts, `Metadata/` for module metadata, limits, and permission code strings, and `Types/` for `CatalogItemStatus` and region code helpers.

## Domain

`CatalogItem` owns:

- tenant id;
- SKU;
- name;
- price;
- currency;
- status: active or discontinued.
- available region codes. An empty list means the item is available in all regions.

Core rules:

- tenant id, SKU, name, price, and currency are required;
- price must be positive;
- price must fit the module's mapped decimal precision without rounding;
- SKU is normalized and unique per tenant;
- SKU, name, and three-letter currency limits are domain invariants before persistence;
- region codes are normalized to uppercase letters/digits/hyphens and capped per item;
- discontinued items cannot be discontinued again.

## Regional Availability

Catalog exposes explicit user-facing availability queries:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/catalog/items/available?region=US` | List active tenant items available in a region. |
| `GET` | `/api/catalog/items/available/{itemId}?region=US` | Get one item only if it is available in a region. |

These endpoints construct an `AccessSubject.User(...)` at the API boundary and read the user's allowed region from the Catalog-owned `catalog_region` claim. `Catalog.Application` adapts those front-door inputs into Catalog domain visibility objects, then `CatalogAvailabilityPolicy` returns an `AvailableCatalogItemsScope` only when the requested `region` matches the viewer's region.

The protected repository methods require `AvailableCatalogItemsScope`, not a loose region string. `Catalog.Persistence.QueryScopes.ApplyAvailableCatalogItemsScope(...)` translates that scope into tenant, active-status, and region-availability SQL filters. This keeps list/detail paths from loading broad tenant data and filtering it in memory.

This is the preferred example for resource access that affects persistence:

```text
API claims/query
  -> AccessSubject + CatalogViewer
  -> CatalogAvailabilityPolicy
  -> AvailableCatalogItemsScope
  -> ICatalogItemReadRepository.ListAvailableAsync(scope, page, ct)
```

## Cache

Queries use explicit cache-aside through `IApplicationCache`.

Keys:

- `catalog:item:{itemId}`
- `catalog:items:{page}:{pageSize}`
- `catalog:available-item:{itemId}:{regionCode}`
- `catalog:available-items:{regionCode}:{page}:{pageSize}`

Tags:

- `catalog:items`

Create/update/discontinue commands enqueue invalidation through `ICacheInvalidationQueue`. Cache data is non-authoritative and tenant-scoped, so the default Catalog profile requires the generic `caching.tenant-scope` composition feature. Hosts satisfy that through `Gma.Framework.Tenancy.Caching`.

## Admin Permissions

Permission code strings live in `Catalog.Contracts` for module metadata. Typed `AdminPermission` constants live in `Catalog.Admin.Contracts` for CLI/API front doors.

```text
catalog.items.read
catalog.items.create
catalog.items.update
catalog.items.discontinue
```

## Integration Events

| Event | Subject |
| --- | --- |
| `CatalogItemCreatedIntegrationEvent` | `{application-namespace}.catalog.item-created.v1` |
| `CatalogItemUpdatedIntegrationEvent` | `{application-namespace}.catalog.item-updated.v1` |
| `CatalogItemDiscontinuedIntegrationEvent` | `{application-namespace}.catalog.item-discontinued.v1` |

Events are written by domain-event handlers through the module outbox. The local default namespace is `gma`; `CatalogIntegrationSubjects` can render the same logical events under a configured application namespace.

Catalog does not publish user notifications directly. A consuming module decides who is affected by Catalog facts. The compiled Ordering example consumes Catalog item events into a local projection, finds order owners affected by item changes, and publishes `UserNotificationRequestedIntegrationEvent` from Ordering's own outbox. Notifications then stores and streams only already-addressed requests.

## Projection Export

Catalog exposes a producer-owned export contract for rebuild/backfill scenarios:

- `CatalogItemProjectionExport` is the stable snapshot consumed by Ordering rebuilds.
- `ICatalogItemProjectionExportSource` is the contract-facing source port.
- `Catalog.Persistence` implements the port against Catalog's EF model.

Consumers reference `Catalog.Contracts` only. They do not reference Catalog domain, application, persistence, API, or admin projects for rebuilds.

## Compose Explicitly

Add project references from the host to the front doors you want, then register:

```csharp
builder.AddModule<CatalogModule>();
builder.AddAdminModule<CatalogAdminCliModule>();
builder.AddAdminApiModule<CatalogAdminApiModule>();
```

Only add the registrations to hosts that should expose Catalog.
