namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class DisableMemberCommandValidator : ICommandValidator<DisableMemberCommand>
{
    public IEnumerable<string> Validate(DisableMemberCommand command)
    {
        if (command.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            yield return "Disable reason is required.";
        }
    }
}
