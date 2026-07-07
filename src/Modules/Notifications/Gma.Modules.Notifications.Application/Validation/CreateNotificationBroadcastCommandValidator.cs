namespace Gma.Modules.Notifications.Application.Validation;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using ContractAudience = Gma.Modules.Notifications.Contracts.NotificationBroadcastAudience;
using ContractSeverity = Gma.Modules.Notifications.Contracts.NotificationSeverity;

internal sealed class CreateNotificationBroadcastCommandValidator : ICommandValidator<CreateNotificationBroadcastCommand>
{
    public IEnumerable<string> Validate(CreateNotificationBroadcastCommand command)
    {
        if (command.Audience == ContractAudience.Unknown)
        {
            yield return "Notification broadcast audience is required.";
        }

        if (command.Audience is ContractAudience.TenantUsers or ContractAudience.TenantAdmins &&
            string.IsNullOrWhiteSpace(command.TenantId))
        {
            yield return "Tenant-scoped notification broadcasts require a tenant id.";
        }

        if (command.Audience is ContractAudience.PlatformUsers or ContractAudience.PlatformAdmins &&
            !string.IsNullOrWhiteSpace(command.TenantId))
        {
            yield return "Platform notification broadcasts cannot include a tenant id.";
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            yield return "Notification broadcast name is required.";
        }

        if (command.Version <= 0)
        {
            yield return "Notification broadcast version must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            yield return "Notification broadcast title is required.";
        }

        if (command.Severity is ContractSeverity.Unknown ||
            !Enum.IsDefined(command.Severity))
        {
            yield return "Notification broadcast severity is invalid.";
        }

        if (string.IsNullOrWhiteSpace(command.PayloadJson))
        {
            yield return "Notification broadcast payload JSON is required.";
        }
        else if (command.PayloadJson.Length > NotificationPayload.MaxLength)
        {
            yield return $"Notification broadcast payload JSON must be {NotificationPayload.MaxLength} characters or fewer.";
        }
    }
}
