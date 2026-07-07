namespace Gma.Modules.Administration.AdminApi;

using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Queries;
using Gma.Modules.Administration.Contracts;
using Gma.Modules.Administration.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

public sealed class AdministrationAdminApiModule : IAdminApiModule
{
    public string Name => AdministrationModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddAdministrationApplication(builder.Configuration);
        builder.AddAdministrationPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin")
            .WithModuleName(this.Name)
            .WithTags("Administration Admin")
            .RequireAuthorization();

        RouteGroupBuilder roles = group.MapGroup("/roles");

        roles.MapGet("/", async (
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesList, AdministrationPermissions.RolesRead),
                requireTenant: false,
                token => dispatcher.QueryAsync(new ListRolesQuery(), token),
                cancellationToken).ConfigureAwait(false));

        roles.MapPost("/", async (
            CreateAdminRoleRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesCreate, AdministrationPermissions.RolesManage),
                requireTenant: false,
                token => dispatcher.SendAsync(new CreateRoleCommand(request.Name), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        roles.MapPost("/{roleName}/permissions", async (
            string roleName,
            GrantAdminRolePermissionRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesGrant, AdministrationPermissions.RolesManage),
                requireTenant: false,
                token => dispatcher.SendAsync(new GrantRolePermissionCommand(roleName, request.Permission), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        roles.MapPost("/{roleName}/assignments", async (
            string roleName,
            AssignAdminRoleRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesAssign, AdministrationPermissions.RolesManage),
                requireTenant: false,
                token => dispatcher.SendAsync(new AssignRoleCommand(request.ActorId, roleName, request.TenantId), token),
                cancellationToken,
                tenantId: request.TenantId,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record CreateAdminRoleRequest(string Name);
    public sealed record GrantAdminRolePermissionRequest(string Permission);
    public sealed record AssignAdminRoleRequest(string ActorId, string? TenantId);

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(AdministrationApplicationErrors.RoleNotFound.Code, StatusCodes.Status404NotFound),
        new(AdministrationApplicationErrors.RoleAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(AdministrationApplicationErrors.PermissionAlreadyGranted.Code, StatusCodes.Status409Conflict),
        new(AdministrationApplicationErrors.AssignmentAlreadyExists.Code, StatusCodes.Status409Conflict));
}
