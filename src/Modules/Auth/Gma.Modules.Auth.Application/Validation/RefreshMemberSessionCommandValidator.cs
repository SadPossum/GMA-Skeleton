namespace Gma.Modules.Auth.Application.Validation;

using Gma.Modules.Auth.Application.Commands;
using Gma.Framework.Cqrs;

internal sealed class RefreshMemberSessionCommandValidator : ICommandValidator<RefreshMemberSessionCommand>
{
    public IEnumerable<string> Validate(RefreshMemberSessionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.AccessToken))
        {
            yield return "Access token is required.";
        }

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            yield return "Refresh token is required.";
        }
    }
}
