namespace Gma.Modules.Administration.Application.Validation;

using Gma.Modules.Administration.Application.Commands;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;

internal sealed class GrantRolePermissionCommandValidator : ICommandValidator<GrantRolePermissionCommand>
{
    public IEnumerable<string> Validate(GrantRolePermissionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RoleName))
        {
            yield return "Admin role name is required.";
        }
        else if (!AdminRoleName.TryNormalize(command.RoleName, out _))
        {
            yield return "Admin role name is invalid.";
        }

        if (!AdminPermission.TryCreate(command.PermissionCode, out _))
        {
            yield return "Admin permission code is invalid.";
        }
    }
}
