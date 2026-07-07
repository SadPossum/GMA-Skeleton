namespace Integration.Tests;

using System.Net;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using DotNet.Testcontainers.Containers;
using Integration.Tests.Support;
using Testcontainers.MsSql;
using Xunit;

public sealed class TenantIsolationIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Members_and_sessions_are_isolated_by_tenant()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await nats.StartAsync();
        await sqlServer.StartAsync();

        await using AuthTestApplication application = new(
            "SqlServer",
            AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_auth_tests"),
            AuthTestContainers.GetNatsConnectionString(nats));

        await application.MigrateDatabaseAsync();
        using HttpClient client = application.CreateClient();

        await AuthApiClient.RegisterAsync(client, "tenant-a", "shared@example.com");

        HttpResponseMessage isolatedLogin = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-b",
            "/api/auth/login",
            new LoginMemberRequest("shared@example.com", AuthApiClient.Password));

        string isolatedLoginBody = await isolatedLogin.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Unauthorized, isolatedLogin.StatusCode);
        Assert.Contains(AuthDomainErrors.CredentialsNotValid.Code, isolatedLoginBody, StringComparison.Ordinal);

        AuthTokensResponse tenantBTokens = await AuthApiClient.RegisterAsync(client, "tenant-b", "shared@example.com");

        Assert.False(string.IsNullOrWhiteSpace(tenantBTokens.AccessToken));

        AuthTokensResponse tenantATokens = await AuthApiClient.LoginAsync(client, "tenant-a", "shared@example.com");
        using HttpResponseMessage mismatchedRefresh = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-b",
            "/api/auth/refresh",
            new RefreshTokenRequest(tenantATokens.AccessToken, tenantATokens.RefreshToken));

        string mismatchedRefreshBody = await mismatchedRefresh.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Forbidden, mismatchedRefresh.StatusCode);
        Assert.Contains(AuthApplicationErrors.TenantMismatch.Code, mismatchedRefreshBody, StringComparison.Ordinal);
    }
}
