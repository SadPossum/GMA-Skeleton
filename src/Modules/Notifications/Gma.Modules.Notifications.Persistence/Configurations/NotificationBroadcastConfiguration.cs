namespace Gma.Modules.Notifications.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Naming;

internal sealed class NotificationBroadcastConfiguration : IEntityTypeConfiguration<NotificationBroadcast>
{
    public void Configure(EntityTypeBuilder<NotificationBroadcast> builder)
    {
        builder.ToTable("notification_broadcasts");
        builder.HasKey(broadcast => broadcast.Id);
        builder.Property(broadcast => broadcast.TenantId)
            .HasMaxLength(TenantIds.MaxLength);
        builder.Property(broadcast => broadcast.Audience)
            .HasConversion(
                audience => NotificationBroadcastAudienceNames.ToWireName(audience),
                value => NotificationBroadcastAudienceNames.Parse(value).Value)
            .HasColumnName("Audience")
            .HasMaxLength(NotificationBroadcastAudienceNames.MaxLength)
            .IsRequired();
        builder.OwnsOne(broadcast => broadcast.Source, source =>
        {
            source.Property(value => value.Module)
                .HasColumnName("Module")
                .HasMaxLength(NotificationBroadcast.ModuleMaxLength)
                .IsRequired();
            source.Property(value => value.Name)
                .HasColumnName("Name")
                .HasMaxLength(NotificationBroadcast.NameMaxLength)
                .IsRequired();
            source.Property(value => value.Version)
                .HasColumnName("Version")
                .IsRequired();
            source.HasIndex(value => new { value.Module, value.Name, value.Version });
        });
        builder.Navigation(broadcast => broadcast.Source).IsRequired();
        builder.OwnsOne(broadcast => broadcast.Content, content =>
        {
            content.Property(value => value.Title)
                .HasColumnName("Title")
                .HasMaxLength(NotificationBroadcast.TitleMaxLength)
                .IsRequired();
            content.Property(value => value.Body)
                .HasColumnName("Body")
                .HasMaxLength(NotificationBroadcast.BodyMaxLength);
        });
        builder.Navigation(broadcast => broadcast.Content).IsRequired();
        builder.Property(broadcast => broadcast.Severity)
            .HasConversion(
                severity => NotificationSeverityNames.ToWireName(severity),
                value => NotificationSeverityNames.Parse(value).Value)
            .HasColumnName("Severity")
            .HasMaxLength(NotificationBroadcast.SeverityMaxLength)
            .IsRequired();
        builder.Property(broadcast => broadcast.StreamSequence)
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(broadcast => broadcast.Payload)
            .HasConversion(payload => payload.Json, value => NotificationPayload.Create(value).Value)
            .HasColumnName("PayloadJson")
            .HasMaxLength(NotificationPayload.MaxLength)
            .IsRequired();
        builder.HasIndex(broadcast => new { broadcast.Audience, broadcast.TenantId, broadcast.OccurredAtUtc });
        builder.HasIndex(broadcast => new { broadcast.Audience, broadcast.TenantId, broadcast.StreamSequence });
    }
}
