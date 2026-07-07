namespace Gma.Modules.Administration.Persistence.Configurations;

using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name).HasMaxLength(AdminRoleName.MaxLength).IsRequired();
        builder.HasIndex(role => role.Name).IsUnique();

        builder.HasMany(role => role.Permissions)
            .WithOne()
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(role => role.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(role => role.Assignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
