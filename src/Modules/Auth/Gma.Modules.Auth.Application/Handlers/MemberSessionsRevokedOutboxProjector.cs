namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class MemberSessionsRevokedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<MemberSessionsRevokedDomainEvent>
{
    public Task HandleAsync(MemberSessionsRevokedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(AuthModuleMetadata.Name).EnqueueAsync(
            new MemberSessionsRevokedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.MemberId.Value,
                domainEvent.RevokedSessionCount),
            cancellationToken);
}
