namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class StreamNotificationHistoryQueryValidator : IQueryValidator<StreamNotificationHistoryQuery>
{
    public IEnumerable<string> Validate(StreamNotificationHistoryQuery query)
    {
        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "Notification access subject must be a user.";
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
