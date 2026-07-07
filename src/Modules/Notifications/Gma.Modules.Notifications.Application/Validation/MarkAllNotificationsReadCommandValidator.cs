namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class MarkAllNotificationsReadCommandValidator : ICommandValidator<MarkAllNotificationsReadCommand>
{
    public IEnumerable<string> Validate(MarkAllNotificationsReadCommand command)
    {
        if (command.Subject is null || command.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "Notification access subject must be a user.";
        }
    }
}
