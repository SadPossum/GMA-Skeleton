namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

internal sealed class StreamTenantNotificationHistoryQueryValidator : IQueryValidator<StreamTenantNotificationHistoryQuery>
{
    public IEnumerable<string> Validate(StreamTenantNotificationHistoryQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.UserId) &&
            !NotificationRecipientUserIds.TryNormalize(query.UserId, out _))
        {
            yield return "Notification user id is invalid.";
        }

        if (query.AfterStreamSequence < 0)
        {
            yield return "Notification stream cursor must be zero or greater.";
        }

        if (query.BatchSize is < 1 or > NotificationStreamOptions.MaxBatchSize)
        {
            yield return $"Notification stream batch size must be between 1 and {NotificationStreamOptions.MaxBatchSize}.";
        }
    }
}
