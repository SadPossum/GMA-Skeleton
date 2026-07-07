namespace Gma.Modules.Notifications.Application.Queries;

using Gma.Framework.Cqrs;

public sealed record GetTenantNotificationStreamCursorQuery(string? UserId)
    : IQuery<long>;
