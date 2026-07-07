namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Application.Security;
using Gma.Framework.Cqrs;

internal sealed class ResetMemberPasswordCommandValidator : ICommandValidator<ResetMemberPasswordCommand>
{
    public IEnumerable<string> Validate(ResetMemberPasswordCommand command)
    {
        if (command.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }

        if (!AuthPasswordPolicy.IsValidPlaintextPassword(command.NewPassword))
        {
            yield return AuthPasswordPolicy.MinimumLengthMessage;
        }
    }
}
