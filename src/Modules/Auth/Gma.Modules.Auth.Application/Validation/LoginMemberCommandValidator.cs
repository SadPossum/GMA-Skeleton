namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class LoginMemberCommandValidator : ICommandValidator<LoginMemberCommand>
{
    public IEnumerable<string> Validate(LoginMemberCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            yield return "Username is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            yield return "Password is required.";
        }
    }
}
