namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Queries;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class GetNotificationStreamCursorQueryValidator : IQueryValidator<GetNotificationStreamCursorQuery>
{
    public IEnumerable<string> Validate(GetNotificationStreamCursorQuery query)
    {
        if (query.Subject is null || query.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "Notification access subject must be a user.";
        }
    }
}
