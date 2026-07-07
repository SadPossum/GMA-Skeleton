namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetTenantNotificationHistoryItemQuery(Guid NotificationId)
    : IQuery<AdminNotificationHistoryItem>;
