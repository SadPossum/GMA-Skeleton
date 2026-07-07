namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Visibility;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetNotificationStreamCursorQueryHandler(
    INotificationHistoryRepository repository)
    : IQueryHandler<GetNotificationStreamCursorQuery, long>
{
    public async Task<Result<long>> HandleAsync(
        GetNotificationStreamCursorQuery query,
        CancellationToken cancellationToken)
    {
        if (!NotificationHistoryAccess.CanAccessUserHistory(query.Subject, query.Subject.TenantId))
        {
            return Result.Failure<long>(NotificationsApplicationErrors.AccessDenied);
        }

        long cursor = await repository
            .GetCurrentStreamSequenceForUserAsync(query.Subject, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(cursor);
    }
}
