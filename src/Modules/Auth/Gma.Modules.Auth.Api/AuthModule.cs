namespace Gma.Modules.Auth.Api;

using System.Text.Json;
using System.Security.Claims;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Infrastructure;
using Gma.Modules.Auth.Infrastructure.JwtBearer;
using Gma.Modules.Auth.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using Gma.Framework.Results;

public sealed class AuthModule(AuthProfile profile) : IModule
{
    private readonly AuthProfile profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public AuthModule()
        : this(AuthProfile.TenantScoped())
    {
    }

    public string Name => AuthModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        AddProfileServices(builder, this.profile);
        builder.Services.AddAuthApplication(builder.Configuration);
        builder.Services.AddAuthInfrastructure(builder.Configuration);
        builder
            .AddAuthJwtBearerAuthentication()
            .AddAuthPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        bool requireTenant = this.profile.RequiresTenantContext;
        RouteGroupBuilder group = endpoints.MapGroup("/api/auth")
            .WithModuleName(this.Name)
            .WithTags("Auth");

        RouteHandlerBuilder register = group.MapPost("/register", async (
            RegisterMemberApiRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new RegisterMemberCommand(
                    request.Username,
                    UsernameTypeInput.FromJsonElement(request.UsernameType).Value,
                    request.Password),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes));
        RequireTenantWhenNeeded(register, requireTenant);

        RouteHandlerBuilder login = group.MapPost("/login", async (
            LoginMemberRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new LoginMemberCommand(request.Username, request.Password),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes));
        RequireTenantWhenNeeded(login, requireTenant);

        RouteHandlerBuilder refresh = group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new RefreshMemberSessionCommand(request.AccessToken, request.RefreshToken),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes));
        RequireTenantWhenNeeded(refresh, requireTenant);

        RouteHandlerBuilder signOut = group.MapPost("/sign-out", async (
            SignOutRequest request,
            ClaimsPrincipal user,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!this.TokenTenantMatches(user, tenantContext))
            {
                return Results.Unauthorized();
            }

            Guid? memberId = GetMemberId(user);

            if (memberId is null)
            {
                return Results.Unauthorized();
            }

            Result<Unit> result = await dispatcher.SendAsync(
                new SignOutCommand(memberId.Value, request.RefreshToken),
                cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireAuthorization();
        RequireTenantWhenNeeded(signOut, requireTenant);

        RouteHandlerBuilder signOutAll = group.MapPost("/sign-out-all", async (
            ClaimsPrincipal user,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            if (!this.TokenTenantMatches(user, tenantContext))
            {
                return Results.Unauthorized();
            }

            Guid? memberId = GetMemberId(user);

            if (memberId is null)
            {
                return Results.Unauthorized();
            }

            Result<Unit> result = await dispatcher.SendAsync(
                new SignOutAllCommand(memberId.Value),
                cancellationToken).ConfigureAwait(false);

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireAuthorization();
        RequireTenantWhenNeeded(signOutAll, requireTenant);
    }

    private static void AddProfileServices(IHostApplicationBuilder builder, AuthProfile profile)
    {
        builder.SelectModuleProfile(profile.Descriptor, "Gma.Modules.Auth.Api");

        if (!profile.RequiresTenantContext &&
            !string.IsNullOrWhiteSpace(profile.GlobalScopeId))
        {
            builder.Services.PostConfigure<TenantOptions>(options => options.LocalDefaultTenantId = profile.GlobalScopeId);
        }
    }

    private static void RequireTenantWhenNeeded(RouteHandlerBuilder builder, bool requireTenant)
    {
        if (requireTenant)
        {
            builder.RequireTenant();
        }
    }

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(AuthApplicationErrors.CredentialsNotValid.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.TokenInvalid.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.MemberNotFound.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.SessionNotFound.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.SessionInactive.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.RefreshTokenInvalid.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.RefreshTokenExpired.Code, StatusCodes.Status401Unauthorized),
        new(AuthApplicationErrors.TenantMismatch.Code, StatusCodes.Status403Forbidden),
        new(AuthApplicationErrors.MemberStatusUnknown.Code, StatusCodes.Status403Forbidden),
        new(AuthApplicationErrors.MemberDisabled.Code, StatusCodes.Status403Forbidden),
        new(AuthApplicationErrors.UsernameAlreadyExists.Code, StatusCodes.Status409Conflict));

    public sealed record RegisterMemberApiRequest(string Username, JsonElement UsernameType, string Password);

    private static Guid? GetMemberId(ClaimsPrincipal user)
    {
        string? memberId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(memberId, out Guid parsed)
            ? parsed
            : null;
    }

    private bool TokenTenantMatches(ClaimsPrincipal user, ITenantContext tenantContext)
    {
        if (!this.profile.RequiresTenantContext)
        {
            return true;
        }

        if (!tenantContext.IsEnabled)
        {
            return true;
        }

        string? tokenTenantId = user.FindFirstValue(ApplicationClaimNames.TenantId);

        return !string.IsNullOrWhiteSpace(tokenTenantId) &&
               string.Equals(tokenTenantId, tenantContext.TenantId, StringComparison.Ordinal);
    }
}

public static class AuthModuleHostBuilderExtensions
{
    public static IHostApplicationBuilder AddAuthModule(this IHostApplicationBuilder builder, AuthProfile profile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);

        return builder.AddModule(new AuthModule(profile));
    }
}
