namespace Gma.Modules.Files.Application.Validation;

using Gma.Modules.Files.Application.Commands;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class UploadFileCommandValidator : ICommandValidator<UploadFileCommand>
{
    public IEnumerable<string> Validate(UploadFileCommand command)
    {
        if (command.Content is null || !command.Content.CanRead)
        {
            yield return "A readable file stream is required.";
        }

        if (command.ContentLength <= 0)
        {
            yield return "File content length must be greater than zero.";
        }

        if (command.Subject is null || command.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }
    }
}
