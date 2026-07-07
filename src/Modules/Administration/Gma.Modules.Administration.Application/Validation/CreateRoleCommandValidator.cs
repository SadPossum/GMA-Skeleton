namespace Gma.Modules.Administration.Application.Validation;

using Gma.Modules.Administration.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class CreateRoleCommandValidator : ICommandValidator<CreateRoleCommand>
{
    public IEnumerable<string> Validate(CreateRoleCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            yield return "Admin role name is required.";
        }
        else if (!AdminRoleName.TryNormalize(command.Name, out _))
        {
            yield return "Admin role name is invalid.";
        }
    }
}
