namespace Gma.Modules.Files.Application.Handlers;

using Gma.Modules.Files.Application.Commands;
using Gma.Modules.Files.Contracts;
using Microsoft.Extensions.Options;
using Gma.Framework.Cqrs;
using Gma.Framework.FileManagement;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tenancy;
using Gma.Modules.Files.Application.Visibility;

internal sealed class UploadFileCommandHandler(
    IFileStorage storage,
    IIdGenerator idGenerator,
    ITenantContext tenantContext,
    IOptions<FileManagementOptions> fileManagementOptions)
    : ICommandHandler<UploadFileCommand, FileUploadResponse>
{
    public async Task<Result<FileUploadResponse>> HandleAsync(
        UploadFileCommand command,
        CancellationToken cancellationToken)
    {
        Result<Unit> access = FilesAccess.EnsureUserSubject(command.Subject, tenantContext);
        if (access.IsFailure)
        {
            return Result.Failure<FileUploadResponse>(access.Error);
        }

        if (command.Content is null || !command.Content.CanRead)
        {
            return Result.Failure<FileUploadResponse>(FilesApplicationErrors.FileRequired);
        }

        if (command.ContentLength <= 0)
        {
            return Result.Failure<FileUploadResponse>(FilesApplicationErrors.FileEmpty);
        }

        FileManagementOptions options = fileManagementOptions.Value;
        if (command.ContentLength > options.MaximumObjectBytes)
        {
            return Result.Failure<FileUploadResponse>(FilesApplicationErrors.FileTooLarge);
        }

        string contentType = FileStorageMetadata.ContentTypeOrDefault(command.ContentType);
        if (options.AllowedContentTypes.Length > 0 &&
            !options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<FileUploadResponse>(FilesApplicationErrors.ContentTypeNotAllowed);
        }

        Guid fileId = idGenerator.NewId();
        if (fileId == Guid.Empty)
        {
            return Result.Failure<FileUploadResponse>(FilesApplicationErrors.FileIdInvalid);
        }

        FileStorageObjectKey key = FilesStorageKeys.For(fileId, command.Subject, tenantContext);
        FileStorageWriteRequest request = new(
            key,
            command.Content,
            command.ContentLength,
            contentType,
            command.FileName,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["module"] = "files"
            });

        FileStorageObjectProperties stored = await storage.PutAsync(request, cancellationToken).ConfigureAwait(false);
        FileUploadResponse response = new(
            fileId,
            stored.ContentType,
            stored.ContentLength,
            stored.FileName,
            $"/api/files/{fileId:D}");

        return Result.Success(response);
    }
}
