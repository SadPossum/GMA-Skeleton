namespace Gma.Modules.Files.Application.Validation;

using Gma.Modules.Files.Application.Commands;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

internal sealed class DeleteFileCommandValidator : ICommandValidator<DeleteFileCommand>
{
    public IEnumerable<string> Validate(DeleteFileCommand command)
    {
        if (command.FileId == Guid.Empty)
        {
            yield return "File id is required.";
        }

        if (command.Subject is null || command.Subject.Kind != AccessSubjectKind.User)
        {
            yield return "A user access subject is required.";
        }
    }
}
