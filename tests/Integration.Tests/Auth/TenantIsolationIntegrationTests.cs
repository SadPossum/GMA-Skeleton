namespace Integration.Tests;

using System.Net;
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
    public async Task Global_members_and_sessions_are_independent_of_product_tenant_headers()
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

        AuthTokensResponse registeredTokens = await AuthApiClient.RegisterAsync(client, "tenant-a", "shared@example.com");

        using HttpResponseMessage crossTenantLogin = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-b",
            "/api/auth/login",
            new LoginMemberRequest("shared@example.com", AuthApiClient.Password));

        Assert.Equal(HttpStatusCode.OK, crossTenantLogin.StatusCode);

        using HttpResponseMessage duplicateRegistration = await AuthApiClient.PostJsonAsync(
            client,
            "tenant-b",
            "/api/auth/register",
            new RegisterMemberRequest("shared@example.com", UsernameType.Email, AuthApiClient.Password));
        string duplicateRegistrationBody = await duplicateRegistration.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRegistration.StatusCode);
        Assert.Contains(AuthDomainErrors.UsernameAlreadyExists.Code, duplicateRegistrationBody, StringComparison.Ordinal);

        AuthTokensResponse refreshedTokens = await AuthApiClient.RefreshAsync(client, "tenant-b", registeredTokens);
        Assert.False(string.IsNullOrWhiteSpace(refreshedTokens.AccessToken));
    }
}
