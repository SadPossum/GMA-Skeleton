namespace Gma.Modules.Administration.Tests;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Persistence.Entities;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminRbacEntityTests
{
    private static readonly Guid Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid RoleId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 2, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Principal_normalizes_actor_id()
    {
        AdminPrincipal principal = new(" Actor-1 ", CreatedAtUtc);

        Assert.Equal("Actor-1", principal.Id);
        Assert.Equal(CreatedAtUtc, principal.CreatedAtUtc);
    }

    [Fact]
    public void Principal_rejects_blank_actor_id()
    {
        Assert.Throws<ArgumentException>(() => new AdminPrincipal(" ", CreatedAtUtc));
        Assert.Throws<ArgumentException>(() => new AdminPrincipal("actor 1", CreatedAtUtc));
    }

    [Fact]
    public void Principal_role_normalizes_actor_and_tenant_ids()
    {
        AdminPrincipalRole assignment = new(Id, " Actor-1 ", RoleId, " tenant-a ", CreatedAtUtc);

        Assert.Equal("Actor-1", assignment.PrincipalId);
        Assert.Equal("tenant-a", assignment.TenantId);
        Assert.Equal(RoleId, assignment.RoleId);
    }

    [Fact]
    public void Principal_role_uses_empty_global_tenant_scope()
    {
        AdminPrincipalRole assignment = new(Id, "actor", RoleId, " ", CreatedAtUtc);

        Assert.Equal(string.Empty, assignment.TenantId);
    }

    [Fact]
    public void Principal_role_rejects_invalid_actor_and_tenant_ids()
    {
        Assert.Throws<ArgumentException>(() => new AdminPrincipalRole(Id, " ", RoleId, "tenant-a", CreatedAtUtc));
        Assert.Throws<ArgumentException>(() => new AdminPrincipalRole(Id, "actor 1", RoleId, "tenant-a", CreatedAtUtc));
        Assert.Throws<ArgumentException>(() => new AdminPrincipalRole(Id, "actor", RoleId, new string('x', TenantIds.MaxLength + 1), CreatedAtUtc));
    }

    [Fact]
    public void Role_permission_normalizes_permission_code()
    {
        AdminRolePermission permission = new(Id, RoleId, " Auth.Members.Read ", CreatedAtUtc);

        Assert.Equal("auth.members.read", permission.PermissionCode);
        Assert.Equal(RoleId, permission.RoleId);
    }

    [Fact]
    public void Role_permission_allows_owner_wildcard()
    {
        AdminRolePermission permission = new(Id, RoleId, " * ", CreatedAtUtc);

        Assert.Equal("*", permission.PermissionCode);
    }

    [Fact]
    public void Role_permission_rejects_invalid_permission_code()
    {
        Assert.Throws<ArgumentException>(() => new AdminRolePermission(Id, RoleId, "auth", CreatedAtUtc));
    }
}
