namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class MemberDisabledOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<MemberDisabledDomainEvent>
{
    public Task HandleAsync(MemberDisabledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(AuthModuleMetadata.Name).EnqueueAsync(
            new MemberDisabledIntegrationEvent(
                domainEvent.EventId,
                domainEvent.TenantId,
                domainEvent.OccurredAtUtc,
                domainEvent.MemberId.Value,
                domainEvent.Reason),
            cancellationToken);
}
