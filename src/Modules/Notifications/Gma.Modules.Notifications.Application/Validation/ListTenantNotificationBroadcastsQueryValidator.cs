namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.Cqrs;
using Gma.Framework.Naming;

internal sealed class ListTenantNotificationBroadcastsQueryValidator
    : IQueryValidator<ListTenantNotificationBroadcastsQuery>
{
    public IEnumerable<string> Validate(ListTenantNotificationBroadcastsQuery query)
    {
        if (!TenantIds.TryNormalize(query.TenantId, out _))
        {
            yield return "Notification broadcast tenant id is invalid.";
        }
    }
}
