namespace Gma.Modules.Notifications.Contracts;

using Gma.Framework.Messaging;

public static class NotificationsIntegrationSubjects
{
    public static string CreateUserNotificationRequested(
        string producerModule,
        string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            subjectPrefix,
            producerModule,
            UserNotificationRequestedIntegrationEvent.EventType,
            UserNotificationRequestedIntegrationEvent.EventVersion);
}
