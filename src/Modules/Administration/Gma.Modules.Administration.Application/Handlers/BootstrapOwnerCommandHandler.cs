namespace Gma.Modules.Administration.Application.Handlers;

using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Ports;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class BootstrapOwnerCommandHandler(
    IAdminRbacRepository repository,
    IOptions<AdministrationOptions> options,
    ISystemClock clock)
    : ICommandHandler<BootstrapOwnerCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(BootstrapOwnerCommand command, CancellationToken cancellationToken)
    {
        if (!command.Confirmed)
        {
            return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
        }

        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.ActorRequired);
        }

        if (!AdminActor.TrySystem(command.ActorId, out AdminActor? actor))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.ActorInvalid);
        }

        bool hasAssignments = await repository.HasAnyAssignmentsAsync(cancellationToken).ConfigureAwait(false);

        if (hasAssignments && !options.Value.Bootstrap.AllowWhenAssignmentsExist)
        {
            return Result.Failure<Unit>(AdminErrors.BootstrapNotAllowed);
        }

        if (!AdminRoleName.TryNormalize(options.Value.Bootstrap.OwnerRoleName, out string? roleName))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNameInvalid);
        }

        DateTimeOffset nowUtc = clock.UtcNow;

        await repository.EnsurePrincipalAsync(actor.Id, nowUtc, cancellationToken).ConfigureAwait(false);
        await repository.EnsureRoleAsync(roleName, nowUtc, cancellationToken).ConfigureAwait(false);
        await repository.EnsureRolePermissionAsync(roleName, AdminPermission.OwnerWildcard, nowUtc, cancellationToken).ConfigureAwait(false);
        await repository.EnsureRoleAssignmentAsync(actor.Id, roleName, null, nowUtc, cancellationToken).ConfigureAwait(false);

        return Result.Success(Unit.Value);
    }
}
