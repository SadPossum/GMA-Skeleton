namespace Gma.Modules.Administration.Persistence.Configurations;

using Gma.Modules.Administration.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Administration;

internal sealed class AdminPrincipalConfiguration : IEntityTypeConfiguration<AdminPrincipal>
{
    public void Configure(EntityTypeBuilder<AdminPrincipal> builder)
    {
        builder.ToTable("principals");
        builder.HasKey(principal => principal.Id);

        builder.Property(principal => principal.Id).HasMaxLength(AdminActor.MaxLength).IsRequired();

        builder.HasMany(principal => principal.Roles)
            .WithOne()
            .HasForeignKey(role => role.PrincipalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(principal => principal.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
