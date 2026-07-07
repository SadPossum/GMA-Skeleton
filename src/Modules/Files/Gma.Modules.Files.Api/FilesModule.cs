namespace Gma.Modules.Files.Api;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using Gma.Modules.Files.Application;
using Gma.Modules.Files.Application.Commands;
using Gma.Modules.Files.Application.Queries;
using Gma.Modules.Files.Application.ReadModels;
using Gma.Modules.Files.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.AccessControl;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;

public sealed class FilesModule : IModule
{
    private const string MaximumObjectBytesConfigurationKey = "FileManagement:MaximumObjectBytes";
    private const long DefaultMaximumObjectBytes = 10 * 1024 * 1024;

    public string Name => FilesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(FilesProfiles.Default, "Gma.Modules.Files.Api");
        builder.Services.AddFilesApplication(builder.Configuration);
        ConfigureMultipartLimits(builder);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/files")
            .WithModuleName(this.Name)
            .WithTags("Files")
            .RequireAuthorization();

        group.MapPost("/", async (
            IFormFile? file,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, tenantContext, out AccessSubject? subject, out IResult? failure))
            {
                return failure;
            }

            if (file is null)
            {
                return Result.Failure<FileUploadResponse>(FilesApplicationErrors.FileRequired)
                    .ToHttpResult(PublicErrorStatusCodes);
            }

            await using Stream stream = file.OpenReadStream();
            Result<FileUploadResponse> result = await dispatcher.SendAsync(
                new UploadFileCommand(stream, file.Length, file.ContentType, file.FileName, subject),
                cancellationToken).ConfigureAwait(false);

            return result.IsFailure
                ? result.ToHttpResult(PublicErrorStatusCodes)
                : Results.Created(result.Value.DownloadPath, result.Value);
        })
            .RequireTenant()
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapGet("/{fileId:guid}", async (
            Guid fileId,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, tenantContext, out AccessSubject? subject, out IResult? failure))
            {
                return failure;
            }

            Result<FileDownload> result = await dispatcher.QueryAsync(
                new GetFileQuery(fileId, subject),
                cancellationToken).ConfigureAwait(false);

            return result.IsFailure
                ? result.ToHttpResult(PublicErrorStatusCodes)
                : new FileDownloadHttpResult(result.Value);
        })
            .RequireTenant()
            .RequireAuthorization();

        group.MapDelete("/{fileId:guid}", async (
            Guid fileId,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, tenantContext, out AccessSubject? subject, out IResult? failure))
            {
                return failure;
            }

            Result<Unit> result = await dispatcher.SendAsync(
                new DeleteFileCommand(fileId, subject),
                cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();
    }

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(FilesApplicationErrors.TenantRequired.Code, StatusCodes.Status400BadRequest),
        new(FilesApplicationErrors.FileRequired.Code, StatusCodes.Status400BadRequest),
        new(FilesApplicationErrors.FileEmpty.Code, StatusCodes.Status400BadRequest),
        new(FilesApplicationErrors.FileTooLarge.Code, StatusCodes.Status413PayloadTooLarge),
        new(FilesApplicationErrors.ContentTypeNotAllowed.Code, StatusCodes.Status415UnsupportedMediaType),
        new(FilesApplicationErrors.FileIdInvalid.Code, StatusCodes.Status400BadRequest),
        new(FilesApplicationErrors.FileNotFound.Code, StatusCodes.Status404NotFound),
        new(FilesApplicationErrors.AccessDenied.Code, StatusCodes.Status403Forbidden));

    private static bool TryResolveUserSubject(
        HttpContext httpContext,
        ITenantContext tenantContext,
        [NotNullWhen(true)] out AccessSubject? subject,
        [NotNullWhen(false)] out IResult? failure)
    {
        subject = null;
        failure = null;

        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                         httpContext.User.FindFirstValue(ApplicationClaimNames.Subject);
        if (string.IsNullOrWhiteSpace(userId))
        {
            failure = Results.Unauthorized();
            return false;
        }

        string? tenantId = tenantContext.TenantId;
        if (tenantContext.IsEnabled)
        {
            string? tokenTenantId = httpContext.User.FindFirstValue(ApplicationClaimNames.TenantId);
            if (!TenantIds.TryNormalize(tokenTenantId, out string? normalizedTokenTenantId) ||
                !string.Equals(normalizedTokenTenantId, tenantContext.TenantId, StringComparison.Ordinal))
            {
                failure = Results.Forbid();
                return false;
            }

            tenantId = normalizedTokenTenantId;
        }

        if (!AccessSubject.TryCreate(AccessSubjectKind.User, userId, tenantId, out subject))
        {
            failure = Results.Unauthorized();
            return false;
        }

        return true;
    }

    private static void ConfigureMultipartLimits(IHostApplicationBuilder builder)
    {
        string? configuredMaximum = builder.Configuration[MaximumObjectBytesConfigurationKey];
        long maximumObjectBytes =
            long.TryParse(configuredMaximum, NumberStyles.Integer, CultureInfo.InvariantCulture, out long configured) &&
            configured > 0
                ? configured
                : DefaultMaximumObjectBytes;

        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = maximumObjectBytes;
        });
    }

    private sealed class FileDownloadHttpResult(FileDownload download) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = download.File.Properties.ContentType;
            httpContext.Response.ContentLength = download.File.Properties.ContentLength;
            await download.File.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted)
                .ConfigureAwait(false);
        }
    }
}
