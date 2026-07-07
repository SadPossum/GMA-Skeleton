namespace Gma.Modules.Auth.Domain.Events;

using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain;

public sealed record MemberSessionsRevokedDomainEvent : TenantDomainEvent
{
    public MemberSessionsRevokedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAtUtc,
        MemberId memberId,
        string tenantId,
        int revokedSessionCount)
        : base(eventId, occurredAtUtc, tenantId)
    {
        _ = DomainEventGuards.RequireId(memberId.Value, nameof(memberId));
        this.MemberId = memberId;
        this.RevokedSessionCount = DomainEventGuards.RequirePositive(revokedSessionCount, nameof(revokedSessionCount));
    }

    public MemberId MemberId { get; }
    public int RevokedSessionCount { get; }
}
