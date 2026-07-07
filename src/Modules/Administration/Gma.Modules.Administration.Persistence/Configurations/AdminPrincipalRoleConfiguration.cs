namespace Gma.Modules.Administration.Persistence.Configurations;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Administration;

internal sealed class AdminPrincipalRoleConfiguration : IEntityTypeConfiguration<AdminPrincipalRole>
{
    public void Configure(EntityTypeBuilder<AdminPrincipalRole> builder)
    {
        builder.ToTable("principal_roles");
        builder.HasKey(role => role.Id);

        builder.Property(role => role.PrincipalId).HasMaxLength(AdminActor.MaxLength).IsRequired();
        builder.Property(role => role.TenantId).HasMaxLength(TenantIds.MaxLength).IsRequired();

        builder.HasOne(role => role.Role)
            .WithMany(role => role.Assignments)
            .HasForeignKey(role => role.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(role => new { role.PrincipalId, role.RoleId, role.TenantId }).IsUnique();
        builder.HasIndex(role => new { role.PrincipalId, role.TenantId });
    }
}
