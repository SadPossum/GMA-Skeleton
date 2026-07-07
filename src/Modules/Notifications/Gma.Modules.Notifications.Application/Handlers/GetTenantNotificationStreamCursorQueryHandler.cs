namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetTenantNotificationStreamCursorQueryHandler(INotificationHistoryRepository repository)
    : IQueryHandler<GetTenantNotificationStreamCursorQuery, long>
{
    public async Task<Result<long>> HandleAsync(
        GetTenantNotificationStreamCursorQuery query,
        CancellationToken cancellationToken)
    {
        long cursor = await repository
            .GetCurrentStreamSequenceForTenantAsync(query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(cursor);
    }
}
