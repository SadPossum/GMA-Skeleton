# Administration Module

The Administration module is optional. It owns persisted admin RBAC and audit for hosts that choose to expose administration capabilities.

It does not own feature-specific administration behavior. Feature modules expose their own `.AdminCli` and `.AdminApi` front doors and declare their own permission codes.

## Projects

```text
Gma.Modules.Administration.Application
Gma.Modules.Administration.Persistence
Gma.Modules.Administration.Persistence.SqlServerMigrations
Gma.Modules.Administration.Persistence.PostgreSqlMigrations
Gma.Modules.Administration.AdminCli
Gma.Modules.Administration.AdminApi
```

## Public Contracts

The module currently has no separate `.Contracts` project. Shared administration contracts live in `Gma.Framework.Administration`.

Application-facing records include:

- `AdminRoleDetails`
- `BootstrapOwnerCommand`
- `CreateRoleCommand`
- `GrantRolePermissionCommand`
- `AssignRoleCommand`
- `ListRolesQuery`

## CLI Commands

Base command:

```text
admin
```

Commands:

- `admin bootstrap --actor <id> --yes`
- `admin roles create --actor <id> --name <role>`
- `admin roles grant --actor <id> --role <role> --permission <code>`
- `admin roles assign --actor <id> --target-actor <id> --role <role> [--tenant <id>]`
- `admin roles list --actor <id> [--output table|json]`

`bootstrap` creates the first owner principal. It succeeds only when there are no existing admin assignments unless configuration explicitly allows bootstrap over existing assignments.

Role names are lowercase slugs. They must start with a letter and may contain lowercase letters, numbers, and hyphens. Invalid role names return normal application errors and are audited.

Bootstrap configuration:

```json
{
  "Administration": {
    "Bootstrap": {
      "AllowWhenAssignmentsExist": false,
      "OwnerRoleName": "owner"
    }
  }
}
```

`Bootstrap:OwnerRoleName` is validated at startup and must follow the same role-name slug rule.

## Admin API

`Gma.Modules.Administration.AdminApi` is optional and is composed by `Host.AdminApi`.

Routes:

- `GET /api/admin/roles`
- `POST /api/admin/roles`
- `POST /api/admin/roles/{roleName}/permissions`
- `POST /api/admin/roles/{roleName}/assignments`

Bootstrap remains CLI-only.

## Permissions

Administration declares:

| Permission | Purpose |
| --- | --- |
| `admin.bootstrap` | First owner bootstrap operation. |
| `admin.roles.read` | List roles and assignments. |
| `admin.roles.manage` | Create roles, grant permissions, and assign roles. |

The bootstrap role receives:

```text
*
```

The wildcard is an owner grant and authorizes all admin operations.

## Application Layer

Application code owns RBAC use cases and depends on `IAdminRbacRepository`.

Use cases:

- bootstrap first owner;
- create role;
- grant permission to role;
- assign role globally or tenant-scoped;
- list roles.

Authorization itself is exposed through `PersistedAdminAuthorizationService`, which implements the shared `IAdminAuthorizationService` contract.

## Persistence

Schema:

```text
admin
```

Migration history table:

```text
admin.__ef_migrations_history
```

Tables:

- `principals`
- `roles`
- `role_permissions`
- `principal_roles`
- `audit_entries`

Global role assignments use an internal empty tenant scope so uniqueness is provider-stable. Public APIs still treat global assignments as `null` tenant scope.

## Audit

`AdminAuditSink` writes records for admin operations:

- actor id;
- tenant id;
- operation;
- permission;
- result;
- error code;
- timestamp.

Audit data is intentionally small and secret-free. Do not add command payloads, passwords, tokens, hashes, or raw exception details to audit records.
Actor ids and audit error codes are bounded operation metadata. Actor ids are case-preserving external identifiers, but they cannot contain whitespace or control characters. Error codes should be stable application or domain codes, not free-form messages.

## Integration Events

The Administration module does not publish integration events in this milestone.

## Tests

Relevant coverage:

- bootstrap option validation in `Gma.Modules.Administration.Tests`;
- permission parsing and deny-by-default authorization in `Gma.Framework.Tests`;
- owner wildcard and tenant-scoped grants through integration tests;
- SQL Server and PostgreSQL migrations through Docker integration tests;
- CLI RBAC and Auth admin flows in `AdminCliIntegrationTests`;
- HTTP RBAC and Auth admin flows in `AdminApiIntegrationTests`;
- architecture tests for `System.CommandLine` isolation and module boundaries.

## Extension Points

Likely future additions:

- admin HTTP API front door composed by a separate host decision;
- audit export/read APIs;
- richer principal metadata;
- role templates for common module permission sets.

Keep the module generic. It should know permission codes, principals, roles, and audit, not Auth internals or product-specific user concepts.
