namespace Ordering.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Ordering.Application;
using Ordering.Application.Commands;
using Ordering.Application.Queries;
using Ordering.Contracts;
using Ordering.Persistence;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using System.Security.Claims;

public sealed class OrderingModule : IModule
{
    public string Name => OrderingModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(OrderingProfiles.Default, "Ordering.Api");
        builder.Services.AddOrderingApplication();
        builder.AddOrderingPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder orders = endpoints.MapGroup("/api/orders")
            .WithModuleName(this.Name)
            .WithTags("Ordering");

        orders.MapGet("/", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, tenantContext, out AccessSubject? subject))
            {
                return Results.Unauthorized();
            }

            Result<OrderListResponse> result = await dispatcher.QueryAsync(
                new ListOrdersQuery(subject, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();

        orders.MapGet("/{orderId:guid}", async (
            Guid orderId,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, tenantContext, out AccessSubject? subject))
            {
                return Results.Unauthorized();
            }

            Result<OrderDto> result = await dispatcher.QueryAsync(
                new GetOrderQuery(orderId, subject),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();

        orders.MapPost("/", async (
            PlaceOrderRequest request,
            HttpContext httpContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!TryResolveUserSubject(httpContext, tenantContext, out AccessSubject? subject))
            {
                return Results.Unauthorized();
            }

            Result<OrderDto> result = await dispatcher.SendAsync(
                new PlaceOrderCommand(request.CatalogItemId, request.Quantity, subject, request.RegionCode),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireAuthorization();
    }

    public sealed record PlaceOrderRequest(Guid CatalogItemId, int Quantity, string RegionCode);

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(OrderingApplicationErrors.OrderNotFound.Code, StatusCodes.Status404NotFound),
        new(OrderingApplicationErrors.CatalogItemUnknown.Code, StatusCodes.Status409Conflict),
        new(OrderingApplicationErrors.CatalogItemDiscontinued.Code, StatusCodes.Status409Conflict),
        new(OrderingApplicationErrors.CatalogItemUnavailableInRegion.Code, StatusCodes.Status409Conflict),
        new(OrderingApplicationErrors.CatalogItemStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(OrderingApplicationErrors.AccessDenied.Code, StatusCodes.Status403Forbidden));

    private static bool TryResolveUserSubject(
        HttpContext httpContext,
        ITenantContext tenantContext,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AccessSubject? subject)
    {
        subject = null;
        string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                         httpContext.User.FindFirstValue(ApplicationClaimNames.Subject);
        return !string.IsNullOrWhiteSpace(userId) &&
               AccessSubject.TryCreate(AccessSubjectKind.User, userId, tenantContext.TenantId, out subject);
    }
}
