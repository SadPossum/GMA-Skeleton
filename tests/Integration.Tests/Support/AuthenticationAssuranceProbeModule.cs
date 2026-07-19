namespace Integration.Tests.Support;

using Gma.Framework.Api.Modules;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Modules.Auth.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

internal sealed class AuthenticationAssuranceProbeModule : IModule
{
    private static readonly AuthenticationAssuranceRequirement PasswordRequirement = new(
        [AuthenticationContextReferences.Password],
        TimeSpan.FromMinutes(10));

    private static readonly AuthenticationAssuranceRequirement MultiFactorRequirement = new(
        [AuthenticationContextReferences.MultiFactor],
        TimeSpan.FromMinutes(10));

    public string Name => "authentication-assurance-probe";

    public void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/integration/authentication-assurance/password",
                static () => Results.NoContent())
            .RequireAuthenticationAssurance(PasswordRequirement);

        endpoints.MapGet(
                "/api/integration/authentication-assurance/mfa",
                static () => Results.NoContent())
            .RequireAuthenticationAssurance(MultiFactorRequirement);
    }
}
