namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class MemberRegisteredOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<MemberRegisteredDomainEvent>
{
    public Task HandleAsync(MemberRegisteredDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(AuthModuleMetadata.Name).EnqueueAsync(
            new MemberRegisteredIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.MemberId.Value,
                domainEvent.Username),
            cancellationToken);
}
