namespace Gma.Modules.Notifications.Application.Handlers;

using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class MarkNotificationReadCommandHandler(
    INotificationHistoryRepository repository,
    ISystemClock clock)
    : ICommandHandler<MarkNotificationReadCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        bool updated = await repository
            .MarkReadAsync(command.NotificationId, command.Subject, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return updated
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(NotificationsDomainErrors.NotificationNotFound);
    }
}
