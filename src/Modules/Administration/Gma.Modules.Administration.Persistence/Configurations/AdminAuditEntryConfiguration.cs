namespace Gma.Modules.Administration.Persistence.Configurations;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Administration;

internal sealed class AdminAuditEntryConfiguration : IEntityTypeConfiguration<AdminAuditEntry>
{
    public void Configure(EntityTypeBuilder<AdminAuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.ActorId).HasMaxLength(AdminActor.MaxLength).IsRequired();
        builder.Property(entry => entry.TenantId).HasMaxLength(TenantIds.MaxLength);
        builder.Property(entry => entry.Operation).HasMaxLength(AdminOperation.MaxLength).IsRequired();
        builder.Property(entry => entry.Permission).HasMaxLength(AdminPermission.MaxLength).IsRequired();
        builder.Property(entry => entry.Result).HasMaxLength(AdminAuditResults.MaxLength).IsRequired();
        builder.Property(entry => entry.ErrorCode).HasMaxLength(AdminAuditRecord.ErrorCodeMaxLength);

        builder.HasIndex(entry => new { entry.ActorId, entry.CreatedAtUtc });
        builder.HasIndex(entry => new { entry.TenantId, entry.CreatedAtUtc });
    }
}
