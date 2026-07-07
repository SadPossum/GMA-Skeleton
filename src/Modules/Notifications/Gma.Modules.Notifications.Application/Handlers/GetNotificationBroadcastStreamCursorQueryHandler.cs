namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetNotificationBroadcastStreamCursorQueryHandler(INotificationBroadcastRepository repository)
    : IQueryHandler<GetNotificationBroadcastStreamCursorQuery, long>
{
    public async Task<Result<long>> HandleAsync(
        GetNotificationBroadcastStreamCursorQuery query,
        CancellationToken cancellationToken)
    {
        Result<NotificationBroadcastRecipientContext> recipient =
            NotificationBroadcastRecipientContext.Create(query.TenantId, query.RecipientKind, query.RecipientId);
        if (recipient.IsFailure)
        {
            return Result.Failure<long>(recipient.Error);
        }

        long cursor = await repository
            .GetCurrentStreamSequenceAsync(recipient.Value, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(cursor);
    }
}
