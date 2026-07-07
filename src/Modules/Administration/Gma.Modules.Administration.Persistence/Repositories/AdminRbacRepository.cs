namespace Gma.Modules.Administration.Persistence.Repositories;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Application.Ports;
using Gma.Modules.Administration.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Administration;
using Gma.Framework.Runtime.Identity;

internal sealed class AdminRbacRepository(AdminDbContext dbContext, IIdGenerator idGenerator) : IAdminRbacRepository
{
    private const string GlobalTenantScope = "";

    public Task<bool> HasAnyAssignmentsAsync(CancellationToken cancellationToken) =>
        dbContext.PrincipalRoles.AnyAsync(cancellationToken);

    public Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken)
    {
        string normalizedRoleName = AdminRole.NormalizeName(roleName);
        return dbContext.Roles.AnyAsync(role => role.Name == normalizedRoleName, cancellationToken);
    }

    public async Task<bool> RoleHasPermissionAsync(
        string roleName,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        string normalizedRoleName = AdminRole.NormalizeName(roleName);
        string normalizedPermission = NormalizePermission(permissionCode);

        return await dbContext.RolePermissions
            .AnyAsync(permission =>
                permission.PermissionCode == normalizedPermission &&
                dbContext.Roles.Any(role => role.Id == permission.RoleId && role.Name == normalizedRoleName),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> AssignmentExistsAsync(
        string actorId,
        string roleName,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        string normalizedRoleName = AdminRole.NormalizeName(roleName);
        string normalizedTenantId = NormalizeTenantId(tenantId);
        string normalizedActorId = NormalizeActorId(actorId);

        return await dbContext.PrincipalRoles
            .AnyAsync(assignment =>
                assignment.PrincipalId == normalizedActorId &&
                assignment.TenantId == normalizedTenantId &&
                assignment.Role != null &&
                assignment.Role.Name == normalizedRoleName,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasPermissionAsync(
        string actorId,
        string permissionCode,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        string normalizedPermission = NormalizePermission(permissionCode);
        string normalizedTenantId = NormalizeTenantId(tenantId);
        string normalizedActorId = NormalizeActorId(actorId);

        return await dbContext.PrincipalRoles
            .AnyAsync(assignment =>
                assignment.PrincipalId == normalizedActorId &&
                (assignment.TenantId == GlobalTenantScope || assignment.TenantId == normalizedTenantId) &&
                assignment.Role != null &&
                assignment.Role.Permissions.Any(permission =>
                    permission.PermissionCode == AdminPermission.OwnerWildcard ||
                    permission.PermissionCode == normalizedPermission),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task EnsurePrincipalAsync(
        string actorId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        string normalizedActorId = NormalizeActorId(actorId);

        if (await dbContext.Principals.AnyAsync(principal => principal.Id == normalizedActorId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.Principals.Add(new AdminPrincipal(normalizedActorId, createdAtUtc));
    }

    public async Task EnsureRoleAsync(
        string roleName,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        string normalizedRoleName = AdminRole.NormalizeName(roleName);

        if (await dbContext.Roles.AnyAsync(role => role.Name == normalizedRoleName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.Roles.Add(new AdminRole(idGenerator.NewId(), normalizedRoleName, createdAtUtc));
    }

    public async Task EnsureRolePermissionAsync(
        string roleName,
        string permissionCode,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (await this.RoleHasPermissionAsync(roleName, permissionCode, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        AdminRole role = await this.GetRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        dbContext.RolePermissions.Add(new AdminRolePermission(idGenerator.NewId(), role.Id, NormalizePermission(permissionCode), createdAtUtc));
    }

    public async Task EnsureRoleAssignmentAsync(
        string actorId,
        string roleName,
        string? tenantId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (await this.AssignmentExistsAsync(actorId, roleName, tenantId, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await this.AssignRoleAsync(actorId, roleName, tenantId, createdAtUtc, cancellationToken).ConfigureAwait(false);
    }

    public Task<AdminRoleDetails> CreateRoleAsync(
        string roleName,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        AdminRole role = new(idGenerator.NewId(), roleName, createdAtUtc);
        dbContext.Roles.Add(role);

        return Task.FromResult(new AdminRoleDetails(role.Id, role.Name, [], 0));
    }

    public async Task GrantRolePermissionAsync(
        string roleName,
        string permissionCode,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        AdminRole role = await this.GetRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        dbContext.RolePermissions.Add(new AdminRolePermission(idGenerator.NewId(), role.Id, NormalizePermission(permissionCode), createdAtUtc));
    }

    public async Task AssignRoleAsync(
        string actorId,
        string roleName,
        string? tenantId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        AdminRole role = await this.GetRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        dbContext.PrincipalRoles.Add(new AdminPrincipalRole(idGenerator.NewId(), NormalizeActorId(actorId), role.Id, NormalizeTenantId(tenantId), createdAtUtc));
    }

    public async Task<IReadOnlyList<AdminRoleDetails>> ListRolesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .AsNoTracking()
            .Include(role => role.Permissions)
            .Include(role => role.Assignments)
            .OrderBy(role => role.Name)
            .Select(role => new AdminRoleDetails(
                role.Id,
                role.Name,
                role.Permissions
                    .OrderBy(permission => permission.PermissionCode)
                    .Select(permission => permission.PermissionCode)
                    .ToArray(),
                role.Assignments.Count))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AdminRole> GetRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        string normalizedRoleName = AdminRole.NormalizeName(roleName);
        AdminRole? localRole = dbContext.Roles.Local.SingleOrDefault(role => role.Name == normalizedRoleName);

        if (localRole is not null)
        {
            return localRole;
        }

        return await dbContext.Roles
            .SingleAsync(role => role.Name == normalizedRoleName, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string NormalizePermission(string permissionCode) =>
        permissionCode == AdminPermission.OwnerWildcard
            ? AdminPermission.OwnerWildcard
            : AdminPermission.Create(permissionCode).Code;

    private static string NormalizeActorId(string actorId) => AdminActor.System(actorId).Id;

    private static string NormalizeTenantId(string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) ? GlobalTenantScope : TenantIds.Normalize(tenantId);
}
