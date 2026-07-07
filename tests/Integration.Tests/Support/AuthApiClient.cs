namespace Integration.Tests.Support;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gma.Modules.Auth.Contracts;
using Xunit;

internal static class AuthApiClient
{
    public const string Password = "Passw0rd!integration";
    private const string TenantHeader = "X-Tenant-Id";

    public static async Task<AuthTokensResponse> RegisterAsync(HttpClient client, string tenantId, string username)
    {
        using HttpResponseMessage response = await PostJsonAsync(
            client,
            tenantId,
            "/api/auth/register",
            new RegisterMemberRequest(username, UsernameType.Email, Password)).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        AuthTokensResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>()
            .ConfigureAwait(false);

        Assert.NotNull(tokens);
        return tokens;
    }

    public static async Task<AuthTokensResponse> LoginAsync(HttpClient client, string tenantId, string username)
    {
        using HttpResponseMessage response = await PostJsonAsync(
            client,
            tenantId,
            "/api/auth/login",
            new LoginMemberRequest(username, Password)).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        AuthTokensResponse? tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>()
            .ConfigureAwait(false);

        Assert.NotNull(tokens);
        return tokens;
    }

    public static async Task<AuthTokensResponse> RefreshAsync(
        HttpClient client,
        string tenantId,
        AuthTokensResponse tokens)
    {
        using HttpResponseMessage response = await PostJsonAsync(
            client,
            tenantId,
            "/api/auth/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken)).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        AuthTokensResponse? refreshed = await response.Content.ReadFromJsonAsync<AuthTokensResponse>()
            .ConfigureAwait(false);

        Assert.NotNull(refreshed);
        return refreshed;
    }

    public static async Task<HttpResponseMessage> PostJsonAsync<TValue>(
        HttpClient client,
        string tenantId,
        string path,
        TValue value,
        string? bearerToken = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        request.Headers.Add(TenantHeader, tenantId);

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        request.Content = JsonContent.Create(value);
        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
