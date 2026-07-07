namespace Gma.Modules.Auth.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record MemberEnabledIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "member-enabled";
    public const int EventVersion = 1;

    public MemberEnabledIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid memberId)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
        => this.MemberId = IntegrationEventContractGuards.RequireId(memberId, nameof(memberId));

    public Guid MemberId { get; }
}
