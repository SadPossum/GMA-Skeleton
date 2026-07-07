namespace Gma.Modules.Auth.AdminApi;

using System.Text.Json;
using Gma.Modules.Auth.Admin.Contracts;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Application.Queries;
using Gma.Modules.Auth.Application.Security;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Infrastructure;
using Gma.Modules.Auth.Infrastructure.JwtBearer;
using Gma.Modules.Auth.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Observability;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Tenancy;
using Gma.Framework.Results;

public sealed class AuthAdminApiModule(AuthProfile profile) : IAdminApiModule
{
    private readonly AuthProfile profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public AuthAdminApiModule()
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
        RouteGroupBuilder members = endpoints.MapGroup("/api/admin/auth/members")
            .WithModuleName(this.Name)
            .WithTags("Auth Admin")
            .RequireAuthorization();

        members.MapGet("/", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersList, AuthAdminPermissions.MembersRead),
                requireTenant,
                token => dispatcher.QueryAsync(
                    new ListAdminMembersQuery(page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        members.MapGet("/{memberId:guid}", async (
            Guid memberId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersGet, AuthAdminPermissions.MembersRead),
                requireTenant,
                token => dispatcher.QueryAsync(new GetAdminMemberQuery(memberId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        members.MapPost("/", async (
            CreateAdminMemberRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            IOptions<AdminApiOptions> adminApiOptions,
            CancellationToken cancellationToken) =>
        {
            return await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersCreate, AuthAdminPermissions.MembersCreate),
                requireTenant,
                token => CreateMemberAsync(dispatcher, request, adminApiOptions.Value, token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false);
        });

        members.MapPost("/{memberId:guid}/disable", async (
            Guid memberId,
            DisableAdminMemberRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            return await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersDisable, AuthAdminPermissions.MembersDisable),
                requireTenant,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new DisableMemberCommand(memberId, request.Reason), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false);
        });

        members.MapPost("/{memberId:guid}/enable", async (
            Guid memberId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersEnable, AuthAdminPermissions.MembersEnable),
                requireTenant,
                token => dispatcher.SendAsync(new EnableMemberCommand(memberId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        members.MapPost("/{memberId:guid}/reset-password", async (
            Guid memberId,
            ResetAdminMemberPasswordRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            IOptions<AdminApiOptions> adminApiOptions,
            CancellationToken cancellationToken) =>
        {
            return await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersResetPassword, AuthAdminPermissions.MembersResetPassword),
                requireTenant,
                token => ResetPasswordAsync(dispatcher, memberId, request, adminApiOptions.Value, token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false);
        });

        members.MapPost("/{memberId:guid}/revoke-sessions", async (
            Guid memberId,
            RevokeAdminMemberSessionsRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            return await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(AuthAdminOperationNames.MembersRevokeSessions, AuthAdminPermissions.MembersRevokeSessions),
                requireTenant,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new RevokeMemberSessionsCommand(memberId), token)
                    : Task.FromResult(Result.Failure<AdminRevokeSessionsResponse>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false);
        });
    }

    private static void AddProfileServices(IHostApplicationBuilder builder, AuthProfile profile)
    {
        builder.SelectModuleProfile(profile.Descriptor, "Gma.Modules.Auth.AdminApi");

        if (!profile.RequiresTenantContext &&
            !string.IsNullOrWhiteSpace(profile.GlobalScopeId))
        {
            builder.Services.PostConfigure<TenantOptions>(options => options.LocalDefaultTenantId = profile.GlobalScopeId);
        }
    }

    private static async Task<Result<AdminCreatedMemberApiResponse>> CreateMemberAsync(
        IRequestDispatcher dispatcher,
        CreateAdminMemberRequest request,
        AdminApiOptions options,
        CancellationToken cancellationToken)
    {
        Result<PasswordInput> password = ResolvePassword(
            request.Password,
            request.GeneratePassword,
            options.AllowGeneratedPasswordResponses);

        if (password.IsFailure)
        {
            return Result.Failure<AdminCreatedMemberApiResponse>(password.Error);
        }

        Result<AdminCreatedMemberResponse> result = await dispatcher.SendAsync(
            new AdminCreateMemberCommand(
                request.Username,
                UsernameTypeInput.FromJsonElement(request.UsernameType).Value,
                password.Value.Password),
            cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<AdminCreatedMemberApiResponse>(result.Error)
            : Result.Success(new AdminCreatedMemberApiResponse(
                result.Value.MemberId,
                result.Value.Username,
                password.Value.Generated ? password.Value.Password : null));
    }

    private static async Task<Result<ResetAdminMemberPasswordResponse>> ResetPasswordAsync(
        IRequestDispatcher dispatcher,
        Guid memberId,
        ResetAdminMemberPasswordRequest request,
        AdminApiOptions options,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed)
        {
            return Result.Failure<ResetAdminMemberPasswordResponse>(AdminErrors.ConfirmationRequired);
        }

        Result<PasswordInput> password = ResolvePassword(
            request.NewPassword,
            request.GeneratePassword,
            options.AllowGeneratedPasswordResponses);

        if (password.IsFailure)
        {
            return Result.Failure<ResetAdminMemberPasswordResponse>(password.Error);
        }

        Result<Unit> result = await dispatcher.SendAsync(new ResetMemberPasswordCommand(memberId, password.Value.Password), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result.Failure<ResetAdminMemberPasswordResponse>(result.Error)
            : Result.Success(new ResetAdminMemberPasswordResponse(password.Value.Generated ? password.Value.Password : null));
    }

    private static Result<PasswordInput> ResolvePassword(
        string? password,
        bool generatePassword,
        bool allowGeneratedPasswordResponse)
    {
        if (generatePassword && !string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure<PasswordInput>(AdminApiErrors.PasswordSourceConflict);
        }

        if (generatePassword && !allowGeneratedPasswordResponse)
        {
            return Result.Failure<PasswordInput>(AdminApiErrors.GeneratedPasswordResponsesDisabled);
        }

        if (generatePassword)
        {
            return Result.Success(new PasswordInput(AdminPasswordGenerator.Generate(), Generated: true));
        }

        return string.IsNullOrWhiteSpace(password)
            ? Result.Failure<PasswordInput>(AdminApiErrors.PasswordRequired)
            : Result.Success(new PasswordInput(password, Generated: false));
    }

    private sealed record PasswordInput(string Password, bool Generated);
    public sealed record CreateAdminMemberRequest(string Username, JsonElement UsernameType, string? Password, bool GeneratePassword);
    public sealed record AdminCreatedMemberApiResponse(Guid MemberId, string Username, string? GeneratedPassword);
    public sealed record DisableAdminMemberRequest(string Reason, bool Confirmed);
    public sealed record ResetAdminMemberPasswordRequest(string? NewPassword, bool GeneratePassword, bool Confirmed);
    public sealed record ResetAdminMemberPasswordResponse(string? GeneratedPassword);
    public sealed record RevokeAdminMemberSessionsRequest(bool Confirmed);

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(AuthApplicationErrors.MemberNotFound.Code, StatusCodes.Status404NotFound),
        new(AuthApplicationErrors.UsernameAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(AuthApplicationErrors.MemberStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(AuthApplicationErrors.MemberAlreadyDisabled.Code, StatusCodes.Status409Conflict),
        new(AuthApplicationErrors.MemberAlreadyActive.Code, StatusCodes.Status409Conflict));

    private static class AdminApiErrors
    {
        public static readonly Error PasswordRequired = new("Admin.PasswordRequired", "A password is required unless password generation is requested.");
        public static readonly Error PasswordSourceConflict = new("Admin.PasswordSourceConflict", "Provide a password or request password generation, not both.");
        public static readonly Error GeneratedPasswordResponsesDisabled = new(
            "Admin.GeneratedPasswordResponsesDisabled",
            "Generated password responses are disabled for the admin API.");
    }
}

public static class AuthAdminApiModuleHostBuilderExtensions
{
    public static IHostApplicationBuilder AddAuthAdminApiModule(this IHostApplicationBuilder builder, AuthProfile profile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);

        return builder.AddAdminApiModule(new AuthAdminApiModule(profile));
    }
}
