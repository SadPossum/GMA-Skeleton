namespace Gma.Modules.Administration.Tests;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Persistence.Entities;
using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminAuditEntryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 7, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_normalizes_audit_metadata()
    {
        AdminAuditEntry entry = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            " Actor-1 ",
            " tenant-a ",
            " Auth.Members.List ",
            " Auth.Members.Read ",
            " Succeeded ",
            " Auth.Error ",
            CreatedAtUtc);

        Assert.Equal("Actor-1", entry.ActorId);
        Assert.Equal("tenant-a", entry.TenantId);
        Assert.Equal("auth.members.list", entry.Operation);
        Assert.Equal("auth.members.read", entry.Permission);
        Assert.Equal("succeeded", entry.Result);
        Assert.Equal("Auth.Error", entry.ErrorCode);
        Assert.Equal(CreatedAtUtc, entry.CreatedAtUtc);
    }

    [Fact]
    public void Constructor_copies_validated_audit_record()
    {
        AdminAuditRecord record = new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            " Actor-1 ",
            " tenant-a ",
            " Auth.Members.List ",
            " Auth.Members.Read ",
            " Succeeded ",
            " Auth.Error ",
            CreatedAtUtc);

        AdminAuditEntry entry = new(record);

        Assert.Equal(record.Id, entry.Id);
        Assert.Equal(record.ActorId, entry.ActorId);
        Assert.Equal(record.TenantId, entry.TenantId);
        Assert.Equal(record.Operation, entry.Operation);
        Assert.Equal(record.Permission, entry.Permission);
        Assert.Equal(record.ResultName, entry.Result);
        Assert.Equal(record.ErrorCode, entry.ErrorCode);
        Assert.Equal(record.CreatedAtUtc, entry.CreatedAtUtc);
    }

    [Fact]
    public void Constructor_rejects_invalid_operation_name()
    {
        Assert.Throws<ArgumentException>(() => Create(operation: "auth"));
    }

    [Fact]
    public void Constructor_rejects_invalid_actor_id()
    {
        Assert.Throws<ArgumentException>(() => Create(actorId: "actor 1"));
    }

    [Fact]
    public void Constructor_rejects_invalid_permission_code()
    {
        Assert.Throws<ArgumentException>(() => Create(permission: "auth"));
    }

    [Fact]
    public void Constructor_rejects_invalid_tenant_id()
    {
        Assert.Throws<ArgumentException>(() => Create(tenantId: new string('x', TenantIds.MaxLength + 1)));
    }

    private static AdminAuditEntry Create(
        string actorId = "actor",
        string? tenantId = "tenant-a",
        string operation = "auth.members.list",
        string permission = "auth.members.read") =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            actorId,
            tenantId,
            operation,
            permission,
            "succeeded",
            null,
            CreatedAtUtc);
}
