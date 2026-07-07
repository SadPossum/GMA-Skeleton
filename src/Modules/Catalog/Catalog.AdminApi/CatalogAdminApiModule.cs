namespace Catalog.AdminApi;

using Catalog.Admin.Contracts;
using Catalog.Application;
using Catalog.Application.Commands;
using Catalog.Application.Queries;
using Catalog.Contracts;
using Catalog.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

public sealed class CatalogAdminApiModule : IAdminApiModule
{
    public string Name => CatalogModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(CatalogProfiles.Default, "Catalog.AdminApi");
        builder.Services.AddCatalogApplication();
        builder.AddCatalogPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder items = endpoints.MapGroup("/api/admin/catalog/items")
            .WithModuleName(this.Name)
            .WithTags("Catalog Admin")
            .RequireAuthorization();

        items.MapGet("/", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsList, CatalogAdminPermissions.ItemsRead),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListCatalogItemsQuery(page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        items.MapGet("/{itemId:guid}", async (
            Guid itemId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsGet, CatalogAdminPermissions.ItemsRead),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetCatalogItemQuery(itemId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        items.MapPost("/", async (
            CatalogItemWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsCreate, CatalogAdminPermissions.ItemsCreate),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CreateCatalogItemCommand(request.Sku, request.Name, request.Price, request.Currency, request.AvailableRegions),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        items.MapPut("/{itemId:guid}", async (
            Guid itemId,
            CatalogItemWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsUpdate, CatalogAdminPermissions.ItemsUpdate),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new UpdateCatalogItemCommand(itemId, request.Sku, request.Name, request.Price, request.Currency, request.AvailableRegions),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        items.MapPost("/{itemId:guid}/discontinue", async (
            Guid itemId,
            ConfirmedRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            return await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(CatalogAdminOperationNames.ItemsDiscontinue, CatalogAdminPermissions.ItemsDiscontinue),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new DiscontinueCatalogItemCommand(itemId), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false);
        });
    }

    public sealed record CatalogItemWriteRequest(
        string Sku,
        string Name,
        decimal Price,
        string Currency,
        IReadOnlyCollection<string>? AvailableRegions = null);
    public sealed record ConfirmedRequest(bool Confirmed);

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(CatalogApplicationErrors.ItemNotFound.Code, StatusCodes.Status404NotFound),
        new(CatalogApplicationErrors.SkuAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(CatalogApplicationErrors.ItemStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(CatalogApplicationErrors.ItemAlreadyDiscontinued.Code, StatusCodes.Status409Conflict));
}
