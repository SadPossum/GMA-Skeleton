namespace Gma.Modules.Auth.Persistence.Configurations;

using Gma.Modules.Auth.Domain.Entities;
using Gma.Modules.Auth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class MemberSessionConfiguration : IEntityTypeConfiguration<MemberSession>
{
    public void Configure(EntityTypeBuilder<MemberSession> builder)
    {
        builder.ToTable("member_sessions");
        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasConversion(id => id.Value, value => new MemberSessionId(value));

        builder.Property(session => session.MemberId)
            .HasConversion(id => id.Value, value => new MemberId(value));

        builder.Property(session => session.RefreshTokenHash)
            .HasMaxLength(MemberSession.RefreshTokenHashMaxLength)
            .IsRequired();

        builder.HasIndex(session => new { session.TenantId, session.RefreshTokenHash });
    }
}
