namespace Gma.Modules.Administration.Persistence.Entities;

using Gma.Framework.Naming;
using Gma.Framework.Administration;

public sealed class AdminPrincipalRole
{
    private AdminPrincipalRole() { }

    public AdminPrincipalRole(Guid id, string principalId, Guid roleId, string tenantId, DateTimeOffset createdAtUtc)
    {
        this.Id = id;
        this.PrincipalId = AdminActor.System(principalId).Id;
        this.RoleId = roleId;
        this.TenantId = string.IsNullOrWhiteSpace(tenantId)
            ? string.Empty
            : TenantIds.Normalize(tenantId);
        this.CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string PrincipalId { get; private set; } = string.Empty;
    public Guid RoleId { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public AdminRole? Role { get; private set; }
}
