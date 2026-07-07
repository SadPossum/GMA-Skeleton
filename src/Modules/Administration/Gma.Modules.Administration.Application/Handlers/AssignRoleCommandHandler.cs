namespace Gma.Modules.Administration.Application.Handlers;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Ports;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class AssignRoleCommandHandler(IAdminRbacRepository repository, ISystemClock clock)
    : ICommandHandler<AssignRoleCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(AssignRoleCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.ActorRequired);
        }

        if (!AdminActor.TrySystem(command.ActorId, out AdminActor? actor))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.ActorInvalid);
        }

        if (string.IsNullOrWhiteSpace(command.RoleName))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNameRequired);
        }

        if (!AdminRoleName.TryNormalize(command.RoleName, out string? roleName))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNameInvalid);
        }

        string? tenantId = null;
        if (!string.IsNullOrWhiteSpace(command.TenantId) &&
            !TenantIds.TryNormalize(command.TenantId, out tenantId))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.TenantInvalid);
        }

        if (!await repository.RoleExistsAsync(roleName, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNotFound);
        }

        if (await repository.AssignmentExistsAsync(actor.Id, roleName, tenantId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.AssignmentAlreadyExists);
        }

        await repository.EnsurePrincipalAsync(actor.Id, clock.UtcNow, cancellationToken).ConfigureAwait(false);
        await repository.AssignRoleAsync(actor.Id, roleName, tenantId, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Unit.Value);
    }
}
