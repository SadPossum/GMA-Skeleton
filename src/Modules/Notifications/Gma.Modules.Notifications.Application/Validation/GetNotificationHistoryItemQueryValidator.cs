namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class GetNotificationHistoryItemQueryValidator : IQueryValidator<GetNotificationHistoryItemQuery>
{
    public IEnumerable<string> Validate(GetNotificationHistoryItemQuery query)
    {
        if (query.NotificationId == Guid.Empty)
        {
            yield return "Notification id is required.";
        }

        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "Notification access subject must be a user.";
        }
    }
}
