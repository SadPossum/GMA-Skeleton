namespace Gma.Modules.Notifications.Application;

using ContractRecipientKind = Gma.Modules.Notifications.Contracts.NotificationBroadcastRecipientKind;
using DomainRecipientKind = Gma.Modules.Notifications.Domain.ValueObjects.NotificationBroadcastRecipientKind;

internal static class NotificationBroadcastRecipientKindMapper
{
    public static DomainRecipientKind ToDomainValue(ContractRecipientKind recipientKind) =>
        recipientKind switch
        {
            ContractRecipientKind.User => DomainRecipientKind.User,
            ContractRecipientKind.Admin => DomainRecipientKind.Admin,
            _ => DomainRecipientKind.Unknown
        };
}
