namespace Gma.Modules.Notifications.Application;

using ContractAudience = Gma.Modules.Notifications.Contracts.NotificationBroadcastAudience;
using DomainAudience = Gma.Modules.Notifications.Domain.ValueObjects.NotificationBroadcastAudience;

internal static class NotificationBroadcastAudienceMapper
{
    public static DomainAudience ToDomainValue(ContractAudience audience) =>
        audience switch
        {
            ContractAudience.TenantUsers => DomainAudience.TenantUsers,
            ContractAudience.TenantAdmins => DomainAudience.TenantAdmins,
            ContractAudience.PlatformUsers => DomainAudience.PlatformUsers,
            ContractAudience.PlatformAdmins => DomainAudience.PlatformAdmins,
            _ => DomainAudience.Unknown
        };
}
