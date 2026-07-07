namespace Gma.Modules.Notifications.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Naming;

internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("user_notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Recipient)
            .HasConversion(recipient => recipient.UserId, value => NotificationRecipient.Create(value).Value)
            .HasColumnName("UserId")
            .HasMaxLength(UserNotification.UserIdMaxLength)
            .IsRequired();
        builder.OwnsOne(notification => notification.Source, source =>
        {
            source.Property(value => value.Module)
                .HasColumnName("Module")
                .HasMaxLength(UserNotification.ModuleMaxLength)
                .IsRequired();
            source.Property(value => value.Name)
                .HasColumnName("Name")
                .HasMaxLength(UserNotification.NameMaxLength)
                .IsRequired();
            source.Property(value => value.Version)
                .HasColumnName("Version")
                .IsRequired();
            source.HasIndex(value => new { value.Module, value.Name, value.Version });
        });
        builder.Navigation(notification => notification.Source).IsRequired();
        builder.OwnsOne(notification => notification.Content, content =>
        {
            content.Property(value => value.Title)
                .HasColumnName("Title")
                .HasMaxLength(UserNotification.TitleMaxLength)
                .IsRequired();
            content.Property(value => value.Body)
                .HasColumnName("Body")
                .HasMaxLength(UserNotification.BodyMaxLength);
        });
        builder.Navigation(notification => notification.Content).IsRequired();
        builder.Property(notification => notification.Severity)
            .HasConversion(
                severity => NotificationSeverityNames.ToWireName(severity),
                value => NotificationSeverityNames.Parse(value).Value)
            .HasColumnName("Severity")
            .HasMaxLength(UserNotification.SeverityMaxLength)
            .IsRequired();
        builder.Property(notification => notification.StreamSequence)
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(notification => notification.Payload)
            .HasConversion(payload => payload.Json, value => NotificationPayload.Create(value).Value)
            .HasColumnName("PayloadJson")
            .HasMaxLength(NotificationPayload.MaxLength)
            .IsRequired();
        builder.HasIndex(notification => new { notification.TenantId, notification.Recipient, notification.OccurredAtUtc });
        builder.HasIndex(notification => new { notification.TenantId, notification.Recipient, notification.ReadAtUtc });
        builder.HasIndex(notification => new { notification.TenantId, notification.Recipient, notification.StreamSequence });
        builder.HasIndex(notification => new { notification.TenantId, notification.StreamSequence });
    }
}
