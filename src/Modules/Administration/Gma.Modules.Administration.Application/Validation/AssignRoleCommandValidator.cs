namespace Gma.Modules.Administration.Application.Validation;

using Gma.Framework.Naming;
using Gma.Modules.Administration.Application.Commands;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;

internal sealed class AssignRoleCommandValidator : ICommandValidator<AssignRoleCommand>
{
    public IEnumerable<string> Validate(AssignRoleCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            yield return "Admin actor id is required.";
        }
        else if (!AdminActor.TrySystem(command.ActorId, out _))
        {
            yield return "Admin actor id is invalid.";
        }

        if (string.IsNullOrWhiteSpace(command.RoleName))
        {
            yield return "Admin role name is required.";
        }
        else if (!AdminRoleName.TryNormalize(command.RoleName, out _))
        {
            yield return "Admin role name is invalid.";
        }

        if (!string.IsNullOrWhiteSpace(command.TenantId) &&
            !TenantIds.TryNormalize(command.TenantId, out _))
        {
            yield return "Tenant id is invalid.";
        }
    }
}
