namespace Gma.Modules.Files.Contracts;

public sealed record FileUploadResponse(
    Guid FileId,
    string ContentType,
    long ContentLength,
    string? FileName,
    string DownloadPath);
