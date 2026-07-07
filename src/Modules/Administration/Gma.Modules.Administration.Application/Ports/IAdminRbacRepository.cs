namespace Gma.Modules.Administration.Application.Ports;

public interface IAdminRbacRepository
{
    Task<bool> HasAnyAssignmentsAsync(CancellationToken cancellationToken);
    Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken);
    Task<bool> RoleHasPermissionAsync(string roleName, string permissionCode, CancellationToken cancellationToken);
    Task<bool> AssignmentExistsAsync(string actorId, string roleName, string? tenantId, CancellationToken cancellationToken);
    Task<bool> HasPermissionAsync(string actorId, string permissionCode, string? tenantId, CancellationToken cancellationToken);
    Task EnsurePrincipalAsync(string actorId, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task EnsureRoleAsync(string roleName, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task EnsureRolePermissionAsync(string roleName, string permissionCode, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task EnsureRoleAssignmentAsync(string actorId, string roleName, string? tenantId, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task<AdminRoleDetails> CreateRoleAsync(string roleName, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task GrantRolePermissionAsync(string roleName, string permissionCode, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task AssignRoleAsync(string actorId, string roleName, string? tenantId, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminRoleDetails>> ListRolesAsync(CancellationToken cancellationToken);
}
