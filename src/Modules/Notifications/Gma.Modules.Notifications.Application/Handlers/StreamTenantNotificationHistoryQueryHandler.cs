namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StreamTenantNotificationHistoryQueryHandler(INotificationHistoryRepository repository)
    : IQueryHandler<StreamTenantNotificationHistoryQuery, IReadOnlyList<AdminNotificationHistoryItem>>
{
    public async Task<Result<IReadOnlyList<AdminNotificationHistoryItem>>> HandleAsync(
        StreamTenantNotificationHistoryQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminNotificationHistoryItem> items = await repository
            .ListNewForTenantAsync(
                query.UserId,
                query.AfterStreamSequence,
                query.BatchSize,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(items);
    }
}
