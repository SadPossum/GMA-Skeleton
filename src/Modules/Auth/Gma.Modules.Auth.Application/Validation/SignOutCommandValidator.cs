namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class SignOutCommandValidator : ICommandValidator<SignOutCommand>
{
    public IEnumerable<string> Validate(SignOutCommand command)
    {
        if (command.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            yield return "Refresh token is required.";
        }
    }
}
