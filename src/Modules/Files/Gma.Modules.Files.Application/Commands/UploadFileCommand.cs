namespace Gma.Modules.Files.Application.Commands;

using Gma.Modules.Files.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;

public sealed record UploadFileCommand(
    Stream Content,
    long ContentLength,
    string? ContentType,
    string? FileName,
    AccessSubject Subject)
    : ICommand<FileUploadResponse>;
