namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Application.Security;
using Gma.Framework.Cqrs;

internal sealed class AdminCreateMemberCommandValidator : ICommandValidator<AdminCreateMemberCommand>
{
    public IEnumerable<string> Validate(AdminCreateMemberCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            yield return "Username is required.";
        }

        if (!AuthPasswordPolicy.IsValidPlaintextPassword(command.Password))
        {
            yield return AuthPasswordPolicy.MinimumLengthMessage;
        }
    }
}
