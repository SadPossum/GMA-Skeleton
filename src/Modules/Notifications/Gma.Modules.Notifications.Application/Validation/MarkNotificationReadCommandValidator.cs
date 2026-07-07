namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class MarkNotificationReadCommandValidator : ICommandValidator<MarkNotificationReadCommand>
{
    public IEnumerable<string> Validate(MarkNotificationReadCommand command)
    {
        if (command.NotificationId == Guid.Empty)
        {
            yield return "Notification id is required.";
        }

        if (command.Subject is null || command.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "Notification access subject must be a user.";
        }
    }
}
