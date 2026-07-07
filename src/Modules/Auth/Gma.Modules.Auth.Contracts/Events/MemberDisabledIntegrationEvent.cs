namespace Gma.Modules.Auth.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record MemberDisabledIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "member-disabled";
    public const int EventVersion = 1;

    public MemberDisabledIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid memberId,
        string reason)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.MemberId = IntegrationEventContractGuards.RequireId(memberId, nameof(memberId));
        this.Reason = IntegrationEventContractGuards.NormalizeRequiredText(
            reason,
            AuthContractLimits.DisableReasonMaxLength,
            nameof(reason));
    }

    public Guid MemberId { get; }
    public string Reason { get; }
}
