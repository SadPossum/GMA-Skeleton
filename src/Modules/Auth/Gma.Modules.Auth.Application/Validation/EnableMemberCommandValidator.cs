namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class EnableMemberCommandValidator : ICommandValidator<EnableMemberCommand>
{
    public IEnumerable<string> Validate(EnableMemberCommand command)
    {
        if (command.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }
    }
}
