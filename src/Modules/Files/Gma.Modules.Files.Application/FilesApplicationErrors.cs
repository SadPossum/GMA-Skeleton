namespace Gma.Modules.Files.Application;

using Gma.Framework.Results;

public static class FilesApplicationErrors
{
    public static readonly Error TenantRequired = new(
        "Files.TenantRequired",
        "A tenant is required for file operations.");

    public static readonly Error AccessDenied = new(
        "Files.AccessDenied",
        "The current subject cannot access this file.");

    public static readonly Error FileRequired = new(
        "Files.FileRequired",
        "A file is required.");

    public static readonly Error FileEmpty = new(
        "Files.FileEmpty",
        "The uploaded file is empty.");

    public static readonly Error FileTooLarge = new(
        "Files.FileTooLarge",
        "The uploaded file exceeds the configured maximum length.");

    public static readonly Error ContentTypeNotAllowed = new(
        "Files.ContentTypeNotAllowed",
        "The uploaded file content type is not allowed.");

    public static readonly Error FileIdInvalid = new(
        "Files.FileIdInvalid",
        "The file id is invalid.");

    public static readonly Error FileNotFound = new(
        "Files.FileNotFound",
        "The file was not found.");
}
