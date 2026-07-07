namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Framework.AccessControl;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetNotificationHistoryItemQuery(Guid NotificationId, AccessSubject Subject)
    : IQuery<NotificationHistoryItem>;
