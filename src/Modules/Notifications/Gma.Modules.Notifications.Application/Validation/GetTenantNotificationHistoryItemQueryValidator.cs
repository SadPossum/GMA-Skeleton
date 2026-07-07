namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetTenantNotificationHistoryItemQueryValidator : IQueryValidator<GetTenantNotificationHistoryItemQuery>
{
    public IEnumerable<string> Validate(GetTenantNotificationHistoryItemQuery query)
    {
        if (query.NotificationId == Guid.Empty)
        {
            yield return "Notification id is required.";
        }
    }
}
