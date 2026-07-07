namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class MarkNotificationBroadcastReadCommandValidator
    : ICommandValidator<MarkNotificationBroadcastReadCommand>
{
    public IEnumerable<string> Validate(MarkNotificationBroadcastReadCommand command)
    {
        if (command.BroadcastId == Guid.Empty)
        {
            yield return "Notification broadcast id is required.";
        }

        foreach (string failure in NotificationBroadcastValidation.ValidateTenantId(command.TenantId))
        {
            yield return failure;
        }

        foreach (string failure in NotificationBroadcastValidation.ValidateRecipient(
            command.RecipientKind,
            command.RecipientId))
        {
            yield return failure;
        }
    }
}
