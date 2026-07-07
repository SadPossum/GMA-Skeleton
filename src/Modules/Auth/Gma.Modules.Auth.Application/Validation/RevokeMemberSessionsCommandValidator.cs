namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RevokeMemberSessionsCommandValidator : ICommandValidator<RevokeMemberSessionsCommand>
{
    public IEnumerable<string> Validate(RevokeMemberSessionsCommand command)
    {
        if (command.MemberId == Guid.Empty)
        {
            yield return "Member id is required.";
        }
    }
}
