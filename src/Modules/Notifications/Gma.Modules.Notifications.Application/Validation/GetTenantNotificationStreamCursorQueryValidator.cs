namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Framework.Cqrs;

internal sealed class GetTenantNotificationStreamCursorQueryValidator : IQueryValidator<GetTenantNotificationStreamCursorQuery>
{
    public IEnumerable<string> Validate(GetTenantNotificationStreamCursorQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.UserId) &&
            !NotificationRecipientUserIds.TryNormalize(query.UserId, out _))
        {
            yield return "Notification user id is invalid.";
        }
    }
}
