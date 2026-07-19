namespace Integration.Tests;

using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DotNet.Testcontainers.Containers;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using Integration.Tests.Support;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class AuthLifecycleIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Register_login_refresh_and_sign_out_runs_against_sql_server_and_postgre_sql()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        await RunAuthLifecycleAsync(
            "SqlServer",
            AuthTestContainers.GetNatsConnectionString(nats),
            async () =>
            {
                MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await sqlServer.StartAsync();
                return new ProviderLease(
                    sqlServer,
                    AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_auth_tests"));
            });

        await RunAuthLifecycleAsync(
            "PostgreSql",
            AuthTestContainers.GetNatsConnectionString(nats),
            async () =>
            {
                PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("gma_auth_tests")
                    .Build();
                await postgreSql.StartAsync();
                return new ProviderLease(postgreSql, postgreSql.GetConnectionString());
            });
    }

    private static async Task RunAuthLifecycleAsync(
        string provider,
        string natsConnectionString,
        Func<Task<ProviderLease>> createProvider)
    {
        await using ProviderLease providerLease = await createProvider().ConfigureAwait(false);
        string keyRingPath = Path.Combine(
            Path.GetTempPath(),
            "gma-skeleton-auth-data-protection",
            Guid.NewGuid().ToString("N"));

        try
        {
            await using AuthTestApplication application = new(
                provider,
                providerLease.ConnectionString,
                natsConnectionString,
                dataProtectionKeyRingPath: keyRingPath);
            await using AuthTestApplication replica = new(
                provider,
                providerLease.ConnectionString,
                natsConnectionString,
                dataProtectionKeyRingPath: keyRingPath);

            await application.MigrateDatabaseAsync().ConfigureAwait(false);
            using HttpClient client = application.CreateClient();

            string username = $"{provider.ToLowerInvariant()}@example.com";
            using HttpResponseMessage invalidUsernameType = await AuthApiClient.PostJsonAsync(
                client,
                "tenant-auth",
                "/api/auth/register",
                new
                {
                    username = $"{provider.ToLowerInvariant()}-invalid-type@example.com",
                    usernameType = 999,
                    password = AuthApiClient.Password
                }).ConfigureAwait(false);
            string invalidUsernameTypeBody = await invalidUsernameType.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.BadRequest, invalidUsernameType.StatusCode);
            Assert.Contains(AuthApplicationErrors.UsernameTypeInvalid.Code, invalidUsernameTypeBody, StringComparison.Ordinal);

            AuthTokensResponse registered = await AuthApiClient.RegisterAsync(client, "tenant-auth", username).ConfigureAwait(false);
            using HttpResponseMessage passwordAssurance = await AuthApiClient.GetAsync(
                client,
                "tenant-auth",
                "/api/integration/authentication-assurance/password",
                registered.AccessToken).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.NoContent, passwordAssurance.StatusCode);

            using HttpResponseMessage insufficientAssurance = await AuthApiClient.GetAsync(
                client,
                "tenant-auth",
                "/api/integration/authentication-assurance/mfa",
                registered.AccessToken).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.Unauthorized, insufficientAssurance.StatusCode);
            Assert.True(insufficientAssurance.Headers.TryGetValues("WWW-Authenticate", out IEnumerable<string>? challenges));
            string challengeHeader = string.Join(", ", challenges ?? []);
            Assert.Contains("insufficient_user_authentication", challengeHeader, StringComparison.Ordinal);
            Assert.Contains("urn:gma:acr:mfa", challengeHeader, StringComparison.Ordinal);

            using HttpResponseMessage duplicateUsername = await AuthApiClient.PostJsonAsync(
                client,
                "tenant-auth",
                "/api/auth/register",
                new RegisterMemberRequest(username, UsernameType.Email, AuthApiClient.Password)).ConfigureAwait(false);
            string duplicateUsernameBody = await duplicateUsername.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.Conflict, duplicateUsername.StatusCode);
            Assert.Contains(AuthDomainErrors.UsernameAlreadyExists.Code, duplicateUsernameBody, StringComparison.Ordinal);

            AuthTokensResponse loggedIn = await AuthApiClient.LoginAsync(client, "tenant-auth", username).ConfigureAwait(false);
            AuthTokensResponse refreshed = await AuthApiClient.RefreshAsync(client, "tenant-auth", loggedIn).ConfigureAwait(false);

            using HttpResponseMessage signOut = await AuthApiClient.PostJsonAsync(
                client,
                "tenant-auth",
                "/api/auth/sign-out",
                new SignOutRequest(refreshed.RefreshToken),
                refreshed.AccessToken).ConfigureAwait(false);

            Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
            Assert.False(string.IsNullOrWhiteSpace(registered.AccessToken));

            await RunMultiFactorLifecycleAsync(application, replica, client, provider).ConfigureAwait(false);
            await RunBrowserMultiFactorLifecycleAsync(application, provider).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(keyRingPath))
            {
                Directory.Delete(keyRingPath, recursive: true);
            }
        }
    }

    private static async Task RunMultiFactorLifecycleAsync(
        AuthTestApplication application,
        AuthTestApplication replica,
        HttpClient client,
        string provider)
    {
        const string scopeId = "tenant-auth";
        string username = $"{provider.ToLowerInvariant()}-mfa@example.com";
        AuthTokensResponse registered = await AuthApiClient.RegisterAsync(client, scopeId, username)
            .ConfigureAwait(false);

        using HttpResponseMessage enrollmentResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/mfa/totp/enrollment",
            new { },
            registered.AccessToken).ConfigureAwait(false);
        enrollmentResponse.EnsureSuccessStatusCode();
        AssertNoStore(enrollmentResponse);
        TotpEnrollmentResponse? enrollment = await enrollmentResponse.Content
            .ReadFromJsonAsync<TotpEnrollmentResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(enrollment);
        AssertSensitiveValueAbsentFromTransport(enrollmentResponse, enrollment.Secret);

        string activationCode = ComputeTotp(enrollment.Secret, DateTimeOffset.UtcNow);
        using HttpClient replicaClient = replica.CreateClient();
        using HttpResponseMessage activationResponse = await AuthApiClient.PostJsonAsync(
            replicaClient,
            scopeId,
            "/api/auth/mfa/totp/activate",
            new ActivateTotpRequest(activationCode, registered.RefreshToken),
            registered.AccessToken).ConfigureAwait(false);
        activationResponse.EnsureSuccessStatusCode();
        AssertNoStore(activationResponse);
        TotpActivationResponse? activation = await activationResponse.Content
            .ReadFromJsonAsync<TotpActivationResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(activation);
        Assert.Equal(10, activation.RecoveryCodes.Count);
        AssertSensitiveValueAbsentFromTransport(activationResponse, activation.RecoveryCodes[0]);

        using HttpResponseMessage loginResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/login",
            new LoginMemberRequest(username, AuthApiClient.Password)).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Accepted, loginResponse.StatusCode);
        AssertNoStore(loginResponse);
        MultiFactorChallengeResponse? challenge = await loginResponse.Content
            .ReadFromJsonAsync<MultiFactorChallengeResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(challenge);
        Assert.Contains(MultiFactorCodeType.Totp, challenge.AvailableCodeTypes);
        Assert.Contains(MultiFactorCodeType.RecoveryCode, challenge.AvailableCodeTypes);
        AssertSensitiveValueAbsentFromTransport(loginResponse, challenge.ChallengeToken);

        int activeSessionsBeforeCompletion = await application.CountActiveSessionsAsync(username)
            .ConfigureAwait(false);
        CompleteMultiFactorChallengeRequest completionRequest = new(
            challenge.ChallengeToken,
            MultiFactorCodeType.RecoveryCode,
            activation.RecoveryCodes[0]);
        Task<HttpResponseMessage>[] competingCompletions =
        [
            AuthApiClient.PostJsonAsync(
                client,
                scopeId,
                "/api/auth/mfa/challenges/complete",
                completionRequest),
            AuthApiClient.PostJsonAsync(
                client,
                scopeId,
                "/api/auth/mfa/challenges/complete",
                completionRequest),
        ];
        HttpResponseMessage[] completionResponses = await Task.WhenAll(competingCompletions).ConfigureAwait(false);
        try
        {
            Assert.Single(completionResponses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(completionResponses, response =>
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Conflict);
            HttpResponseMessage successfulCompletion = completionResponses
                .Single(response => response.StatusCode == HttpStatusCode.OK);
            AssertNoStore(successfulCompletion);
            AuthTokensResponse? completedTokens = await successfulCompletion.Content
                .ReadFromJsonAsync<AuthTokensResponse>()
                .ConfigureAwait(false);
            Assert.NotNull(completedTokens);

            using HttpResponseMessage multiFactorAssurance = await AuthApiClient.GetAsync(
                client,
                scopeId,
                "/api/integration/authentication-assurance/mfa",
                completedTokens.AccessToken).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.NoContent, multiFactorAssurance.StatusCode);

            foreach (HttpResponseMessage response in completionResponses)
            {
                AssertSensitiveValueAbsentFromTransport(response, challenge.ChallengeToken);
                AssertSensitiveValueAbsentFromTransport(response, activation.RecoveryCodes[0]);
            }
        }
        finally
        {
            foreach (HttpResponseMessage response in completionResponses)
            {
                response.Dispose();
            }
        }

        Assert.Equal(
            activeSessionsBeforeCompletion + 1,
            await application.CountActiveSessionsAsync(username).ConfigureAwait(false));

        using HttpResponseMessage replayLoginResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/login",
            new LoginMemberRequest(username, AuthApiClient.Password)).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Accepted, replayLoginResponse.StatusCode);
        MultiFactorChallengeResponse? replayChallenge = await replayLoginResponse.Content
            .ReadFromJsonAsync<MultiFactorChallengeResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(replayChallenge);
        using HttpResponseMessage replayResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/mfa/challenges/complete",
            new CompleteMultiFactorChallengeRequest(
                replayChallenge.ChallengeToken,
                MultiFactorCodeType.RecoveryCode,
                activation.RecoveryCodes[0])).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    private static async Task RunBrowserMultiFactorLifecycleAsync(
        AuthTestApplication application,
        string provider)
    {
        const string scopeId = "tenant-auth";
        string username = $"{provider.ToLowerInvariant()}-browser-mfa@example.com";
        using HttpClient client = application.CreateClient();
        using HttpResponseMessage registerResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/browser/register",
            new RegisterMemberRequest(username, UsernameType.Email, AuthApiClient.Password)).ConfigureAwait(false);
        registerResponse.EnsureSuccessStatusCode();
        AssertNoStore(registerResponse);
        BrowserAuthResponse? registered = await registerResponse.Content
            .ReadFromJsonAsync<BrowserAuthResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(registered);

        using HttpResponseMessage enrollmentResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/mfa/totp/enrollment",
            new { },
            registered.AccessToken).ConfigureAwait(false);
        enrollmentResponse.EnsureSuccessStatusCode();
        TotpEnrollmentResponse? enrollment = await enrollmentResponse.Content
            .ReadFromJsonAsync<TotpEnrollmentResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(enrollment);

        using HttpResponseMessage activationResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/browser/mfa/totp/activate",
            new BrowserActivateTotpRequest(ComputeTotp(enrollment.Secret, DateTimeOffset.UtcNow)),
            registered.AccessToken).ConfigureAwait(false);
        activationResponse.EnsureSuccessStatusCode();
        AssertNoStore(activationResponse);
        BrowserTotpActivationResponse? activation = await activationResponse.Content
            .ReadFromJsonAsync<BrowserTotpActivationResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(activation);
        AssertSensitiveValueAbsentFromTransport(activationResponse, enrollment.Secret);
        AssertSensitiveValueAbsentFromTransport(activationResponse, activation.RecoveryCodes[0]);

        using HttpResponseMessage loginResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/browser/login",
            new LoginMemberRequest(username, AuthApiClient.Password)).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Accepted, loginResponse.StatusCode);
        AssertNoStore(loginResponse);
        MultiFactorChallengeResponse? challenge = await loginResponse.Content
            .ReadFromJsonAsync<MultiFactorChallengeResponse>()
            .ConfigureAwait(false);
        Assert.NotNull(challenge);
        AssertSensitiveValueAbsentFromTransport(loginResponse, challenge.ChallengeToken);

        using HttpResponseMessage completionResponse = await AuthApiClient.PostJsonAsync(
            client,
            scopeId,
            "/api/auth/browser/mfa/challenges/complete",
            new CompleteMultiFactorChallengeRequest(
                challenge.ChallengeToken,
                MultiFactorCodeType.RecoveryCode,
                activation.RecoveryCodes[0])).ConfigureAwait(false);
        completionResponse.EnsureSuccessStatusCode();
        AssertNoStore(completionResponse);
        AssertSensitiveValueAbsentFromTransport(completionResponse, enrollment.Secret);
        AssertSensitiveValueAbsentFromTransport(completionResponse, challenge.ChallengeToken);
        AssertSensitiveValueAbsentFromTransport(completionResponse, activation.RecoveryCodes[0]);
    }

    private static string ComputeTotp(string encodedSecret, DateTimeOffset nowUtc)
    {
        byte[] secret = DecodeBase32(encodedSecret);
        try
        {
            Span<byte> counter = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(counter, nowUtc.ToUnixTimeSeconds() / 30);
#pragma warning disable CA5350 // RFC 6238 authenticator interoperability uses HMAC-SHA1 here.
            byte[] hash = HMACSHA1.HashData(secret, counter);
#pragma warning restore CA5350
            int offset = hash[^1] & 0x0f;
            int binaryCode = ((hash[offset] & 0x7f) << 24) |
                             (hash[offset + 1] << 16) |
                             (hash[offset + 2] << 8) |
                             hash[offset + 3];
            return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        List<byte> bytes = [];
        int buffer = 0;
        int bitCount = 0;
        foreach (char character in value.Trim().TrimEnd('=').ToUpperInvariant())
        {
            int index = alphabet.IndexOf(character);
            Assert.InRange(index, 0, alphabet.Length - 1);
            buffer = (buffer << 5) | index;
            bitCount += 5;
            if (bitCount < 8)
            {
                continue;
            }

            bitCount -= 8;
            bytes.Add((byte)(buffer >> bitCount));
            buffer &= (1 << bitCount) - 1;
        }

        return [.. bytes];
    }

    private static void AssertNoStore(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains(response.Headers.Pragma, value =>
            string.Equals(value.Name, "no-cache", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertSensitiveValueAbsentFromTransport(
        HttpResponseMessage response,
        string sensitiveValue)
    {
        Assert.DoesNotContain(
            sensitiveValue,
            response.RequestMessage?.RequestUri?.OriginalString ?? string.Empty,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            sensitiveValue,
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.Ordinal);
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
        {
            Assert.DoesNotContain(cookies, cookie => cookie.Contains(sensitiveValue, StringComparison.Ordinal));
        }
    }
}
