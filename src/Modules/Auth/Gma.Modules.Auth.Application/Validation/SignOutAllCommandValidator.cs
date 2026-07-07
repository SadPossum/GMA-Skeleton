namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class SignOutAllCommandValidator : ICommandValidator<SignOutAllCommand>
{
    public IEnumerable<string> Validate(SignOutAllCommand command)
    {
        if (command.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }
    }
}
