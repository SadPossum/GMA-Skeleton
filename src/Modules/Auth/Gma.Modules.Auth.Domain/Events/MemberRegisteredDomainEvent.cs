namespace Gma.Modules.Auth.Domain.Events;

using Gma.Modules.Auth.Domain.Entities;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain;

public sealed record MemberRegisteredDomainEvent : TenantDomainEvent
{
    public MemberRegisteredDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        MemberId memberId,
        string tenantId,
        string username)
        : base(eventId, occurredAtUtc, tenantId)
    {
        _ = DomainEventGuards.RequireId(memberId.Value, nameof(memberId));
        this.MemberId = memberId;
        this.Username = DomainEventGuards.NormalizeRequiredText(username, MemberUsername.ValueMaxLength, nameof(username));
    }

    public MemberId MemberId { get; }
    public string Username { get; }
}
