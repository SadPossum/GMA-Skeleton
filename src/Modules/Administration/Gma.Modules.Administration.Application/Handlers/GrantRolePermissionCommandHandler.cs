namespace Gma.Modules.Administration.Application.Handlers;

using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Ports;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class GrantRolePermissionCommandHandler(IAdminRbacRepository repository, ISystemClock clock)
    : ICommandHandler<GrantRolePermissionCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(GrantRolePermissionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RoleName))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNameRequired);
        }

        if (!AdminRoleName.TryNormalize(command.RoleName, out string? roleName))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNameInvalid);
        }

        if (!await repository.RoleExistsAsync(roleName, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.RoleNotFound);
        }

        if (!AdminPermission.TryCreate(command.PermissionCode, out AdminPermission? permission))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.PermissionCodeInvalid);
        }

        if (await repository.RoleHasPermissionAsync(roleName, permission.Code, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<Unit>(AdministrationApplicationErrors.PermissionAlreadyGranted);
        }

        await repository.GrantRolePermissionAsync(roleName, permission.Code, clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Unit.Value);
    }
}
