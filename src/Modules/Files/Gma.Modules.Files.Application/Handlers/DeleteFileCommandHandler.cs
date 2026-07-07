namespace Gma.Modules.Files.Application.Handlers;

using Gma.Modules.Files.Application.Commands;
using Gma.Modules.Files.Application.Visibility;
using Gma.Framework.Cqrs;
using Gma.Framework.FileManagement;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;

internal sealed class DeleteFileCommandHandler(
    IFileStorage storage,
    ITenantContext tenantContext)
    : ICommandHandler<DeleteFileCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        DeleteFileCommand command,
        CancellationToken cancellationToken)
    {
        Result<Unit> access = FilesAccess.EnsureUserSubject(command.Subject, tenantContext);
        if (access.IsFailure)
        {
            return access;
        }

        if (command.FileId == Guid.Empty)
        {
            return Result.Failure<Unit>(FilesApplicationErrors.FileIdInvalid);
        }

        FileStorageObjectKey key = FilesStorageKeys.For(command.FileId, command.Subject, tenantContext);
        bool deleted = await storage.DeleteAsync(key, cancellationToken).ConfigureAwait(false);

        return deleted
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(FilesApplicationErrors.FileNotFound);
    }
}
