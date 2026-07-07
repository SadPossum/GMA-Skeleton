namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetNotificationBroadcastQueryValidator : IQueryValidator<GetNotificationBroadcastQuery>
{
    public IEnumerable<string> Validate(GetNotificationBroadcastQuery query)
    {
        if (query.BroadcastId == Guid.Empty)
        {
            yield return "Notification broadcast id is required.";
        }

        foreach (string failure in NotificationBroadcastValidation.ValidateTenantId(query.TenantId))
        {
            yield return failure;
        }

        foreach (string failure in NotificationBroadcastValidation.ValidateRecipient(
            query.RecipientKind,
            query.RecipientId))
        {
            yield return failure;
        }
    }
}
