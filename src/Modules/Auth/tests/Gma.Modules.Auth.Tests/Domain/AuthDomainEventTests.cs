namespace Gma.Modules.Auth.Tests;

using Gma.Framework.Naming;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Entities;
using Gma.Modules.Auth.Domain.Events;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthDomainEventTests
{
    private static readonly Guid EventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly MemberId MemberId = new(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registered_event_normalizes_metadata_and_username()
    {
        MemberRegisteredDomainEvent domainEvent = new(
            EventId,
            OccurredAtUtc,
            MemberId,
            " tenant-a ",
            " member@example.com ");

        Assert.Equal(EventId, domainEvent.EventId);
        Assert.Equal(OccurredAtUtc, domainEvent.OccurredAtUtc);
        Assert.Equal(MemberId, domainEvent.MemberId);
        Assert.Equal("tenant-a", domainEvent.TenantId);
        Assert.Equal("member@example.com", domainEvent.Username);
    }

    [Fact]
    public void Registered_event_rejects_invalid_metadata_and_username()
    {
        Assert.Throws<ArgumentException>(() => CreateRegisteredEvent(eventId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => CreateRegisteredEvent(occurredAtUtc: default(DateTimeOffset)));
        Assert.Throws<ArgumentException>(() => new MemberRegisteredDomainEvent(
            EventId,
            OccurredAtUtc,
            default,
            "tenant-a",
            "member@example.com"));
        Assert.Throws<ArgumentException>(() => CreateRegisteredEvent(tenantId: " "));
        Assert.Throws<ArgumentException>(() => CreateRegisteredEvent(tenantId: new string('x', TenantIds.MaxLength + 1)));
        Assert.Throws<ArgumentException>(() => CreateRegisteredEvent(username: " "));
        Assert.Throws<ArgumentException>(() => CreateRegisteredEvent(username: new string('x', MemberUsername.ValueMaxLength + 1)));
    }

    [Fact]
    public void Disabled_event_normalizes_reason()
    {
        MemberDisabledDomainEvent domainEvent = new(
            EventId,
            OccurredAtUtc,
            MemberId,
            "tenant-a",
            " support request ");

        Assert.Equal("support request", domainEvent.Reason);
    }

    [Fact]
    public void Sessions_revoked_event_requires_positive_count()
    {
        MemberSessionsRevokedDomainEvent domainEvent = new(
            EventId,
            OccurredAtUtc,
            MemberId,
            "tenant-a",
            1);

        Assert.Equal(1, domainEvent.RevokedSessionCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemberSessionsRevokedDomainEvent(
            EventId,
            OccurredAtUtc,
            MemberId,
            "tenant-a",
            0));
    }

    [Fact]
    public void Disabled_event_rejects_overlong_reason()
    {
        Assert.Throws<ArgumentException>(() => new MemberDisabledDomainEvent(
            EventId,
            OccurredAtUtc,
            MemberId,
            "tenant-a",
            new string('x', Member.DisabledReasonMaxLength + 1)));
    }

    private static MemberRegisteredDomainEvent CreateRegisteredEvent(
        Guid? eventId = null,
        DateTimeOffset? occurredAtUtc = null,
        MemberId? memberId = null,
        string tenantId = "tenant-a",
        string username = "member@example.com") =>
        new(
            eventId ?? EventId,
            occurredAtUtc ?? OccurredAtUtc,
            memberId ?? MemberId,
            tenantId,
            username);
}
