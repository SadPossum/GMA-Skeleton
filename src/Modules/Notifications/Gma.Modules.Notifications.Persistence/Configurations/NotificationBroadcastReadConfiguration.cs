namespace Gma.Modules.Notifications.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Modules.Notifications.Domain.Entities;
using Gma.Modules.Notifications.Domain.ValueObjects;

internal sealed class NotificationBroadcastReadConfiguration : IEntityTypeConfiguration<NotificationBroadcastRead>
{
    public void Configure(EntityTypeBuilder<NotificationBroadcastRead> builder)
    {
        builder.ToTable("notification_broadcast_reads");
        builder.HasKey(read => read.Id);
        builder.Property(read => read.RecipientScope)
            .HasMaxLength(NotificationBroadcastRead.RecipientScopeMaxLength)
            .IsRequired();
        builder.Property(read => read.RecipientKind)
            .HasConversion(
                kind => NotificationBroadcastRecipientKindNames.ToWireName(kind),
                value => NotificationBroadcastRecipientKindNames.Parse(value).Value)
            .HasColumnName("RecipientKind")
            .HasMaxLength(NotificationBroadcastRecipientKindNames.MaxLength)
            .IsRequired();
        builder.Property(read => read.Recipient)
            .HasConversion(recipient => recipient.UserId, value => NotificationRecipient.Create(value).Value)
            .HasColumnName("RecipientId")
            .HasMaxLength(NotificationBroadcastRead.RecipientIdMaxLength)
            .IsRequired();
        builder.HasIndex(read => new { read.BroadcastId, read.RecipientScope, read.RecipientKind, read.Recipient })
            .IsUnique();
        builder.HasIndex(read => new { read.RecipientScope, read.RecipientKind, read.Recipient, read.BroadcastId });
    }
}
