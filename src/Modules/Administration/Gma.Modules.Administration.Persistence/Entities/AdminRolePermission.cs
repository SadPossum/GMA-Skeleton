namespace Gma.Modules.Administration.Persistence.Entities;

using System.Diagnostics.CodeAnalysis;
using Gma.Framework.Administration;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Role permission is the persisted RBAC concept name.")]
public sealed class AdminRolePermission
{
    private AdminRolePermission() { }

    public AdminRolePermission(Guid id, Guid roleId, string permissionCode, DateTimeOffset createdAtUtc)
    {
        this.Id = id;
        this.RoleId = roleId;
        this.PermissionCode = AdminPermission.Create(permissionCode).Code;
        this.CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RoleId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
