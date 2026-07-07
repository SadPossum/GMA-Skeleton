namespace Gma.Modules.Notifications.Application;

using ContractSeverity = Gma.Modules.Notifications.Contracts.NotificationSeverity;
using DomainSeverity = Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity;

internal static class NotificationSeverityMapper
{
    public static DomainSeverity ToDomainValue(ContractSeverity severity) =>
        severity switch
        {
            ContractSeverity.Info => DomainSeverity.Info,
            ContractSeverity.Success => DomainSeverity.Success,
            ContractSeverity.Warning => DomainSeverity.Warning,
            ContractSeverity.Error => DomainSeverity.Error,
            _ => DomainSeverity.Unknown
        };
}
