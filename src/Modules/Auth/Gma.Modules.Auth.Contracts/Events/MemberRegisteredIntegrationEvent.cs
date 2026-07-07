namespace Gma.Modules.Auth.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record MemberRegisteredIntegrationEvent : TenantIntegrationEvent
{
    public const string EventType = "member-registered";
    public const int EventVersion = 1;

    public MemberRegisteredIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid memberId,
        string username)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.MemberId = IntegrationEventContractGuards.RequireId(memberId, nameof(memberId));
        this.Username = IntegrationEventContractGuards.NormalizeRequiredText(
            username,
            AuthContractLimits.UsernameMaxLength,
            nameof(username));
    }

    public Guid MemberId { get; }
    public string Username { get; }
}
