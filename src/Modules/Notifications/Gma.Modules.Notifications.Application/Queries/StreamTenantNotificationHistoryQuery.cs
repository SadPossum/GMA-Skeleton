namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

public sealed record StreamTenantNotificationHistoryQuery(
    string? UserId,
    long AfterStreamSequence,
    int BatchSize)
    : IQuery<IReadOnlyList<AdminNotificationHistoryItem>>;
