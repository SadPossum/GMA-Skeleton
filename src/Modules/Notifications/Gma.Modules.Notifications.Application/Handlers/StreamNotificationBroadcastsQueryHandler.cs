namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class StreamNotificationBroadcastsQueryHandler(INotificationBroadcastRepository repository)
    : IQueryHandler<StreamNotificationBroadcastsQuery, IReadOnlyList<NotificationBroadcastItem>>
{
    public async Task<Result<IReadOnlyList<NotificationBroadcastItem>>> HandleAsync(
        StreamNotificationBroadcastsQuery query,
        CancellationToken cancellationToken)
    {
        Result<NotificationBroadcastRecipientContext> recipient =
            NotificationBroadcastRecipientContext.Create(query.TenantId, query.RecipientKind, query.RecipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NotificationBroadcastItem>>(recipient.Error);
        }

        IReadOnlyList<NotificationBroadcastItem> broadcasts = await repository
            .ListNewVisibleAsync(
                recipient.Value,
                query.AfterStreamSequence,
                query.BatchSize,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(broadcasts);
    }
}
