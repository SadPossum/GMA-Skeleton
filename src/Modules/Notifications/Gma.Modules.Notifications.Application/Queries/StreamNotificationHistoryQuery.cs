namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record StreamNotificationHistoryQuery(
    AccessSubject Subject,
    long AfterStreamSequence,
    int BatchSize)
    : IQuery<IReadOnlyList<NotificationHistoryItem>>;
