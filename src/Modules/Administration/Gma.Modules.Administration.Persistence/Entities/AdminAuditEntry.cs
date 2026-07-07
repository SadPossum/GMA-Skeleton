namespace Gma.Modules.Administration.Persistence.Entities;

using Gma.Framework.Administration;

public sealed class AdminAuditEntry
{
    private AdminAuditEntry() { }

    public AdminAuditEntry(AdminAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        this.Id = record.Id;
        this.ActorId = record.ActorId;
        this.TenantId = record.TenantId;
        this.Operation = record.Operation;
        this.Permission = record.Permission;
        this.Result = record.ResultName;
        this.ErrorCode = record.ErrorCode;
        this.CreatedAtUtc = record.CreatedAtUtc;
    }

    public AdminAuditEntry(
        Guid id,
        string actorId,
        string? tenantId,
        string operation,
        string permission,
        string result,
        string? errorCode,
        DateTimeOffset createdAtUtc)
        : this(new AdminAuditRecord(
            id,
            actorId,
            tenantId,
            operation,
            permission,
            result,
            errorCode,
            createdAtUtc))
    {
    }

    public Guid Id { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string? TenantId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string Permission { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
