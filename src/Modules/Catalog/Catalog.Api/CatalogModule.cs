namespace Catalog.Api;

using Catalog.Application;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Catalog.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;

public sealed class CatalogModule : IModule
{
    public string Name => CatalogModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(CatalogProfiles.Default, "Catalog.Api");
        builder.Services.AddCatalogApplication();
        builder.AddCatalogPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder items = endpoints.MapGroup("/api/catalog/items")
            .WithModuleName(this.Name)
            .WithTags("Catalog");

        items.MapGet("/", async (
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListCatalogItemsQuery(page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        items.MapGet("/available", async (
            string region,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, out AccessSubject? subject))
            {
                return Results.Unauthorized();
            }

            Result<CatalogItemListResponse> result = await dispatcher.QueryAsync(
                new ListAvailableCatalogItemsQuery(
                    subject,
                    region,
                    CatalogUserClaims.GetCurrentRegionCode(httpContext),
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();

        items.MapGet("/{itemId:guid}", async (
            Guid itemId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<CatalogItemDto> result = await dispatcher.QueryAsync(
                new GetCatalogItemQuery(itemId),
                cancellationToken).ConfigureAwait(false);

            return result.IsFailure ? result.ToHttpResult(PublicErrorStatusCodes) : Results.Ok(result.Value);
        })
            .RequireTenant();

        items.MapGet("/available/{itemId:guid}", async (
            Guid itemId,
            string region,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, out AccessSubject? subject))
            {
                return Results.Unauthorized();
            }

            Result<CatalogItemDto> result = await dispatcher.QueryAsync(
                new GetAvailableCatalogItemQuery(
                    itemId,
                    subject,
                    region,
                    CatalogUserClaims.GetCurrentRegionCode(httpContext)),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();

        items.MapPost("/", async (
            CatalogItemWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreateCatalogItemCommand(
                    request.Sku,
                    request.Name,
                    request.Price,
                    request.Currency,
                    request.AvailableRegions),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireAuthorization();

        items.MapPut("/{itemId:guid}", async (
            Guid itemId,
            CatalogItemWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<CatalogItemDto> result = await dispatcher.SendAsync(
                new UpdateCatalogItemCommand(
                    itemId,
                    request.Sku,
                    request.Name,
                    request.Price,
                    request.Currency,
                    request.AvailableRegions),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();

        items.MapPost("/{itemId:guid}/discontinue", async (
            Guid itemId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Unit> result = await dispatcher.SendAsync(
                new DiscontinueCatalogItemCommand(itemId),
                cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();
    }

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(CatalogApplicationErrors.ItemNotFound.Code, StatusCodes.Status404NotFound),
        new(CatalogApplicationErrors.SkuAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(CatalogApplicationErrors.ItemStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(CatalogApplicationErrors.ItemAlreadyDiscontinued.Code, StatusCodes.Status409Conflict),
        new(CatalogApplicationErrors.AccessDenied.Code, StatusCodes.Status403Forbidden));

    private static bool TryResolveUserSubject(
        HttpContext httpContext,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AccessSubject? subject)
    {
        subject = null;
        string? userId = CatalogUserClaims.GetCurrentUserId(httpContext);
        return !string.IsNullOrWhiteSpace(userId) &&
               AccessSubject.TryCreate(AccessSubjectKind.User, userId, out subject);
    }

    public sealed record CatalogItemWriteRequest(
        string Sku,
        string Name,
        decimal Price,
        string Currency,
        IReadOnlyCollection<string>? AvailableRegions = null);
}
