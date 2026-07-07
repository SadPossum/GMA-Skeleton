namespace Gma.Modules.Administration.Application.Validation;

using Gma.Modules.Administration.Application.Commands;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;

internal sealed class BootstrapOwnerCommandValidator : ICommandValidator<BootstrapOwnerCommand>
{
    public IEnumerable<string> Validate(BootstrapOwnerCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ActorId))
        {
            yield return "Admin actor id is required.";
        }
        else if (!AdminActor.TrySystem(command.ActorId, out _))
        {
            yield return "Admin actor id is invalid.";
        }
    }
}
