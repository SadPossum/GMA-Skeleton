namespace Integration.Tests;

using Gma.Framework.Naming;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gma.Modules.AccessControl.Admin.Contracts;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.Administration.Application;
using Gma.Modules.Auth.Admin.Contracts;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using DotNet.Testcontainers.Containers;
using Integration.Tests.Support;
using Gma.Framework.Administration;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class AdminApiIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Admin_api_is_optional_and_manages_auth_members_against_sql_server_and_postgre_sql()
    {
        await RunAsync(
            "SqlServer",
            async () =>
            {
                MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await sqlServer.StartAsync();
                return new ProviderLease(
                    sqlServer,
                    AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_admin_api_tests"));
            });

        await RunAsync(
            "PostgreSql",
            async () =>
            {
                PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("gma_admin_api_tests")
                    .Build();
                await postgreSql.StartAsync();
                return new ProviderLease(postgreSql, postgreSql.GetConnectionString());
            });
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Public_api_does_not_expose_admin_routes()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_public_api_tests")
            .Build();
        await nats.StartAsync();
        await postgreSql.StartAsync();

        await using AuthTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            AuthTestContainers.GetNatsConnectionString(nats));

        using HttpClient client = application.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/admin/roles").ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task RunAsync(string provider, Func<Task<ProviderLease>> createProvider)
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using ProviderLease providerLease = await createProvider().ConfigureAwait(false);
        await using AdminApiTestApplication application = new(
            provider,
            providerLease.ConnectionString,
            AuthTestContainers.GetNatsConnectionString(nats),
            allowGeneratedPasswordResponses: false);

        await application.MigrateAsync().ConfigureAwait(false);

        using HttpClient anonymousClient = application.CreateClient();
        using HttpResponseMessage bootstrapResponse = await anonymousClient.PostAsJsonAsync(
            "/api/admin/bootstrap",
            new { confirmed = true }).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NotFound, bootstrapResponse.StatusCode);

        Guid ownerId = Guid.NewGuid();
        Guid supportId = Guid.NewGuid();
        await application.SeedOwnerAsync(ownerId).ConfigureAwait(false);

        using HttpClient ownerClient = application.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            application.CreateAccessToken(ownerId, "tenant-admin"));

        int invalidRoleAuditCountBefore = await application
            .CountAuditEntriesAsync(AccessControlAdminOperationNames.RolesCreate, AccessControlApplicationErrors.RoleNameInvalid.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage invalidRole = await ownerClient.PostAsJsonAsync(
                "/api/admin/roles",
                new { name = "support team" })
            .ConfigureAwait(false);
        string invalidRoleBody = await invalidRole.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
        Assert.Contains(RequestValidationErrors.FailedCode, invalidRoleBody, StringComparison.Ordinal);
        Assert.Contains(AccessControlApplicationErrors.RoleNameInvalid.Message, invalidRoleBody, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminErrors.OperationFailed.Code, invalidRoleBody, StringComparison.Ordinal);
        Assert.Equal(
            invalidRoleAuditCountBefore,
            await application
                .CountAuditEntriesAsync(AccessControlAdminOperationNames.RolesCreate, AccessControlApplicationErrors.RoleNameInvalid.Code)
                .ConfigureAwait(false));

        await AssertSuccess(ownerClient.PostAsJsonAsync("/api/admin/roles", new { name = "support" }));
        using HttpResponseMessage duplicateRole = await ownerClient.PostAsJsonAsync(
            "/api/admin/roles",
            new { name = "support" }).ConfigureAwait(false);
        string duplicateRoleBody = await duplicateRole.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRole.StatusCode);
        Assert.Contains(AccessControlApplicationErrors.RoleAlreadyExists.Code, duplicateRoleBody, StringComparison.Ordinal);

        int invalidPermissionAuditCountBefore = await application
            .CountAuditEntriesAsync(AccessControlAdminOperationNames.RolesGrant, AccessControlApplicationErrors.PermissionCodeInvalid.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage invalidPermission = await ownerClient.PostAsJsonAsync(
                "/api/admin/roles/support/permissions",
                new { permission = "auth" })
            .ConfigureAwait(false);
        string invalidPermissionBody = await invalidPermission.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPermission.StatusCode);
        Assert.Contains(RequestValidationErrors.FailedCode, invalidPermissionBody, StringComparison.Ordinal);
        Assert.Contains(AccessControlApplicationErrors.PermissionCodeInvalid.Message, invalidPermissionBody, StringComparison.Ordinal);
        Assert.DoesNotContain(AdminErrors.OperationFailed.Code, invalidPermissionBody, StringComparison.Ordinal);
        Assert.Equal(
            invalidPermissionAuditCountBefore,
            await application
                .CountAuditEntriesAsync(AccessControlAdminOperationNames.RolesGrant, AccessControlApplicationErrors.PermissionCodeInvalid.Code)
                .ConfigureAwait(false));

        await GrantAsync(ownerClient, AuthAdminPermissionCodes.MembersRead);
        using HttpResponseMessage duplicatePermission = await ownerClient.PostAsJsonAsync(
                "/api/admin/roles/support/permissions",
                new { permission = AuthAdminPermissionCodes.MembersRead })
            .ConfigureAwait(false);
        string duplicatePermissionBody = await duplicatePermission.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Conflict, duplicatePermission.StatusCode);
        Assert.Contains(AccessControlApplicationErrors.PermissionAlreadyGranted.Code, duplicatePermissionBody, StringComparison.Ordinal);

        await GrantAsync(ownerClient, AuthAdminPermissionCodes.MembersCreate);
        await GrantAsync(ownerClient, AuthAdminPermissionCodes.MembersDisable);
        await GrantAsync(ownerClient, AuthAdminPermissionCodes.MembersEnable);
        await GrantAsync(ownerClient, AuthAdminPermissionCodes.MembersResetPassword);
        await GrantAsync(ownerClient, AuthAdminPermissionCodes.MembersRevokeSessions);
        await AssertSuccess(ownerClient.PostAsJsonAsync(
            "/api/admin/roles/support/assignments",
            new { actorId = supportId.ToString(), scope = "tenant:tenant-admin" }));

        Guid productUserId = Guid.NewGuid();
        await AssertSuccess(ownerClient.PostAsJsonAsync("/api/admin/roles", new { name = "property-reader" }));
        await AssertSuccess(ownerClient.PostAsJsonAsync(
            "/api/admin/roles/property-reader/permissions",
            new { permission = "properties.read" }));
        await AssertSuccess(ownerClient.PostAsJsonAsync(
            "/api/admin/roles/property-reader/assignments",
            new
            {
                subjectKind = "user",
                subjectId = productUserId.ToString(),
                scope = "tenant:tenant-admin"
            }));

        using HttpResponseMessage assignmentsResponse = await ownerClient
            .GetAsync("/api/admin/roles/property-reader/assignments")
            .ConfigureAwait(false);
        string assignmentsBody = await assignmentsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.OK, assignmentsResponse.StatusCode);
        Assert.Contains("\"subjectKind\":\"user\"", assignmentsBody, StringComparison.Ordinal);
        Assert.Contains(productUserId.ToString(), assignmentsBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"scope\":\"tenant:tenant-admin\"", assignmentsBody, StringComparison.Ordinal);

        string unassignUrl = "/api/admin/roles/property-reader/assignments" +
                             $"?subjectKind=user&subjectId={productUserId}&scope=tenant%3Atenant-admin";
        await AssertSuccess(ownerClient.DeleteAsync(unassignUrl));
        using HttpResponseMessage missingAssignment = await ownerClient.DeleteAsync(unassignUrl).ConfigureAwait(false);
        string missingAssignmentBody = await missingAssignment.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NotFound, missingAssignment.StatusCode);
        Assert.Contains(AccessControlApplicationErrors.AssignmentNotFound.Code, missingAssignmentBody, StringComparison.Ordinal);

        await AssertSuccess(ownerClient.DeleteAsync("/api/admin/roles/property-reader/permissions/properties.read"));
        using HttpResponseMessage missingPermission = await ownerClient
            .DeleteAsync("/api/admin/roles/property-reader/permissions/properties.read")
            .ConfigureAwait(false);
        string missingPermissionBody = await missingPermission.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NotFound, missingPermission.StatusCode);
        Assert.Contains(AccessControlApplicationErrors.PermissionNotGranted.Code, missingPermissionBody, StringComparison.Ordinal);

        string ownerUnassignUrl = "/api/admin/roles/owner/assignments" +
                                  $"?subjectKind=admin-actor&subjectId={ownerId}&scope=global";
        using HttpResponseMessage protectedOwner = await ownerClient.DeleteAsync(ownerUnassignUrl).ConfigureAwait(false);
        string protectedOwnerBody = await protectedOwner.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Conflict, protectedOwner.StatusCode);
        Assert.Contains(AccessControlApplicationErrors.LastOwnerProtected.Code, protectedOwnerBody, StringComparison.Ordinal);

        using HttpClient strangerClient = application.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            application.CreateAccessToken(Guid.NewGuid(), "tenant-admin"));
        strangerClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-admin");
        using HttpResponseMessage denied = await strangerClient.GetAsync("/api/admin/auth/members").ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using HttpClient invalidActorClient = application.CreateClient();
        const string invalidActorId = "invalid actor";
        invalidActorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminApiTestApplication.CreateAccessTokenWithActorClaim(invalidActorId, "tenant-admin"));
        invalidActorClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-admin");
        using HttpResponseMessage invalidActor = await invalidActorClient.GetAsync("/api/admin/auth/members").ConfigureAwait(false);
        string invalidActorBody = await invalidActor.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidActor.StatusCode);
        Assert.Contains(AdminErrors.Unauthorized.Code, invalidActorBody, StringComparison.Ordinal);
        Assert.Equal(0, await application.CountAuditEntriesContainingAsync(invalidActorId).ConfigureAwait(false));

        using HttpResponseMessage malformedUnauthorized = await strangerClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-malformed@example.com",
                usernameType = UsernameType.Email,
                password = "manual-password",
                generatePassword = true
            }).ConfigureAwait(false);
        string malformedUnauthorizedBody = await malformedUnauthorized.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Forbidden, malformedUnauthorized.StatusCode);
        Assert.DoesNotContain("PasswordSourceConflict", malformedUnauthorizedBody, StringComparison.Ordinal);

        using HttpClient supportOtherTenantClient = application.CreateClient();
        supportOtherTenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            application.CreateAccessToken(supportId, "tenant-admin"));
        supportOtherTenantClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-other");
        int tenantClaimMismatchAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, AdminErrors.TenantClaimMismatch.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage crossTenantDenied = await supportOtherTenantClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-other@example.com",
                usernameType = UsernameType.Email,
                generatePassword = true
            }).ConfigureAwait(false);
        string crossTenantDeniedBody = await crossTenantDenied.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, crossTenantDenied.StatusCode);
        Assert.Contains(AdminErrors.TenantClaimMismatch.Code, crossTenantDeniedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedPasswordResponsesDisabled", crossTenantDeniedBody, StringComparison.Ordinal);
        Assert.Equal(
            tenantClaimMismatchAuditCountBefore + 1,
            await application.CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, AdminErrors.TenantClaimMismatch.Code).ConfigureAwait(false));

        using HttpClient invalidTenantClaimClient = application.CreateClient();
        invalidTenantClaimClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminApiTestApplication.CreateAccessTokenWithTenantClaim(supportId, new string('x', ScopeIds.MaxLength + 1)));
        invalidTenantClaimClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-admin");
        int invalidTenantClaimAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersList, AdminErrors.TenantClaimMismatch.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage invalidTenantClaimDenied = await invalidTenantClaimClient
            .GetAsync("/api/admin/auth/members")
            .ConfigureAwait(false);
        string invalidTenantClaimDeniedBody = await invalidTenantClaimDenied.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTenantClaimDenied.StatusCode);
        Assert.Contains(AdminErrors.TenantClaimMismatch.Code, invalidTenantClaimDeniedBody, StringComparison.Ordinal);
        Assert.Equal(
            invalidTenantClaimAuditCountBefore + 1,
            await application.CountAuditEntriesAsync(AuthAdminOperationNames.MembersList, AdminErrors.TenantClaimMismatch.Code).ConfigureAwait(false));

        using HttpClient supportClient = application.CreateClient();
        supportClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            application.CreateAccessToken(supportId, "tenant-admin"));
        supportClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-admin");

        using HttpClient supportWithoutTenantClaimClient = application.CreateClient();
        supportWithoutTenantClaimClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            AdminApiTestApplication.CreateAccessTokenWithoutTenantClaim(supportId));
        supportWithoutTenantClaimClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-admin");
        using HttpResponseMessage missingTenantClaimAllowed = await AssertSuccess(
            supportWithoutTenantClaimClient.GetAsync("/api/admin/auth/members")).ConfigureAwait(false);
        JsonElement missingTenantClaimJson = await ReadJsonAsync(missingTenantClaimAllowed).ConfigureAwait(false);
        Assert.Equal(PageRequest.DefaultPage, missingTenantClaimJson.GetProperty("page").GetInt32());
        Assert.Equal(PageRequest.DefaultPageSize, missingTenantClaimJson.GetProperty("pageSize").GetInt32());

        using HttpResponseMessage hugePagination = await AssertSuccess(
            supportClient.GetAsync($"/api/admin/auth/members?page={int.MaxValue}&pageSize={int.MaxValue}")).ConfigureAwait(false);
        JsonElement hugePaginationJson = await ReadJsonAsync(hugePagination).ConfigureAwait(false);
        Assert.Equal(PageRequest.MaxPage, hugePaginationJson.GetProperty("page").GetInt32());
        Assert.Equal(PageRequest.MaxPageSize, hugePaginationJson.GetProperty("pageSize").GetInt32());

        using HttpResponseMessage missingMember = await supportClient.GetAsync($"/api/admin/auth/members/{Guid.NewGuid()}").ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.NotFound, missingMember.StatusCode);

        int invalidUsernameTypeAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, AuthApplicationErrors.UsernameTypeInvalid.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage invalidUsernameType = await supportClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-invalid-type@example.com",
                usernameType = 999,
                password = "manual-password",
                generatePassword = false
            }).ConfigureAwait(false);
        string invalidUsernameTypeBody = await invalidUsernameType.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, invalidUsernameType.StatusCode);
        Assert.Contains(AuthApplicationErrors.UsernameTypeInvalid.Code, invalidUsernameTypeBody, StringComparison.Ordinal);
        Assert.Equal(
            invalidUsernameTypeAuditCountBefore + 1,
            await application
                .CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, AuthApplicationErrors.UsernameTypeInvalid.Code)
                .ConfigureAwait(false));

        int generatedPasswordDisabledAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, "Admin.GeneratedPasswordResponsesDisabled")
            .ConfigureAwait(false);
        using HttpResponseMessage generatedPasswordDisabled = await supportClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-generated-disabled@example.com",
                usernameType = UsernameType.Email,
                generatePassword = true
            }).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, generatedPasswordDisabled.StatusCode);
        Assert.Equal(
            generatedPasswordDisabledAuditCountBefore + 1,
            await application.CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, "Admin.GeneratedPasswordResponsesDisabled").ConfigureAwait(false));

        using HttpClient supportWithoutTenantClient = application.CreateClient();
        supportWithoutTenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            application.CreateAccessToken(supportId, "tenant-admin"));
        int tenantAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, AdminErrors.TenantRequired.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage missingTenant = await supportWithoutTenantClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-missing-tenant@example.com",
                usernameType = UsernameType.Email,
                generatePassword = true
            }).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, missingTenant.StatusCode);
        Assert.Equal(
            tenantAuditCountBefore + 1,
            await application.CountAuditEntriesAsync(AuthAdminOperationNames.MembersCreate, AdminErrors.TenantRequired.Code).ConfigureAwait(false));

        using HttpRequestMessage multipleTenantRequest = new(HttpMethod.Get, "/api/admin/auth/members");
        multipleTenantRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            application.CreateAccessToken(supportId, "tenant-admin"));
        multipleTenantRequest.Headers.Add("X-Tenant-Id", ["tenant-admin", "tenant-other"]);
        int invalidTenantAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersList, AdminErrors.TenantInvalid.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage multipleTenant = await supportClient.SendAsync(multipleTenantRequest).ConfigureAwait(false);
        string multipleTenantBody = await multipleTenant.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, multipleTenant.StatusCode);
        Assert.Contains(AdminErrors.TenantInvalid.Code, multipleTenantBody, StringComparison.Ordinal);
        Assert.Equal(
            invalidTenantAuditCountBefore + 1,
            await application.CountAuditEntriesAsync(AuthAdminOperationNames.MembersList, AdminErrors.TenantInvalid.Code).ConfigureAwait(false));

        string manualPassword = $"Manual-password-{provider}-1!";
        using HttpResponseMessage created = await AssertSuccess(supportClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-member@example.com",
                usernameType = UsernameType.Email,
                password = manualPassword,
                generatePassword = false
            })).ConfigureAwait(false);
        JsonElement createdJson = await ReadJsonAsync(created).ConfigureAwait(false);
        Guid memberId = createdJson.GetProperty("memberId").GetGuid();
        Assert.Equal(JsonValueKind.Null, createdJson.GetProperty("generatedPassword").ValueKind);
        Assert.Equal(0, await application.CountAuditEntriesContainingAsync(manualPassword).ConfigureAwait(false));
        using HttpResponseMessage duplicateMember = await supportClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-member@example.com",
                usernameType = UsernameType.Email,
                password = manualPassword,
                generatePassword = false
            }).ConfigureAwait(false);
        string duplicateMemberBody = await duplicateMember.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Conflict, duplicateMember.StatusCode);
        Assert.Contains(AuthDomainErrors.UsernameAlreadyExists.Code, duplicateMemberBody, StringComparison.Ordinal);

        using HttpResponseMessage listed = await AssertSuccess(supportClient.GetAsync("/api/admin/auth/members")).ConfigureAwait(false);
        JsonElement listJson = await ReadJsonAsync(listed).ConfigureAwait(false);
        Assert.True(listJson.GetProperty("totalCount").GetInt32() >= 1);

        int confirmationAuditCountBefore = await application
            .CountAuditEntriesAsync(AuthAdminOperationNames.MembersDisable, AdminErrors.ConfirmationRequired.Code)
            .ConfigureAwait(false);
        using HttpResponseMessage missingConfirmation = await supportClient.PostAsJsonAsync(
            $"/api/admin/auth/members/{memberId}/disable",
            new { reason = "support request", confirmed = false }).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.BadRequest, missingConfirmation.StatusCode);
        Assert.Equal(
            confirmationAuditCountBefore + 1,
            await application.CountAuditEntriesAsync(AuthAdminOperationNames.MembersDisable, AdminErrors.ConfirmationRequired.Code).ConfigureAwait(false));

        await AssertSuccess(supportClient.PostAsJsonAsync(
            $"/api/admin/auth/members/{memberId}/disable",
            new { reason = "support request", confirmed = true }));

        await using AdminApiTestApplication generatedPasswordApplication = new(
            provider,
            providerLease.ConnectionString,
            AuthTestContainers.GetNatsConnectionString(nats),
            allowGeneratedPasswordResponses: true);
        using HttpClient generatedPasswordClient = generatedPasswordApplication.CreateClient();
        generatedPasswordClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            generatedPasswordApplication.CreateAccessToken(supportId, "tenant-admin"));
        generatedPasswordClient.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-admin");
        using HttpResponseMessage generatedPasswordCreated = await AssertSuccess(generatedPasswordClient.PostAsJsonAsync(
            "/api/admin/auth/members",
            new
            {
                username = $"{provider.ToLowerInvariant()}-generated-enabled@example.com",
                usernameType = UsernameType.Email,
                generatePassword = true
            })).ConfigureAwait(false);
        JsonElement generatedPasswordJson = await ReadJsonAsync(generatedPasswordCreated).ConfigureAwait(false);
        string? generatedPassword = generatedPasswordJson.GetProperty("generatedPassword").GetString();
        Assert.False(string.IsNullOrWhiteSpace(generatedPassword));
        Assert.Equal(0, await generatedPasswordApplication.CountAuditEntriesContainingAsync(generatedPassword).ConfigureAwait(false));

        Guid backupOwnerId = Guid.NewGuid();
        await AssertSuccess(ownerClient.PostAsJsonAsync("/api/admin/roles", new { name = "backup-owner" }));
        await AssertSuccess(ownerClient.PostAsJsonAsync(
            "/api/admin/roles/backup-owner/permissions",
            new { permission = "*" }));
        await AssertSuccess(ownerClient.PostAsJsonAsync(
            "/api/admin/roles/backup-owner/assignments",
            new
            {
                subjectKind = "admin-actor",
                subjectId = backupOwnerId.ToString(),
                scope = "global"
            }));

        Result<Unit>[] concurrentOwnerRemovals = await Task.WhenAll(
            application.UnassignGlobalAdminOwnerAsync(ownerId, "owner"),
            application.UnassignGlobalAdminOwnerAsync(backupOwnerId, "backup-owner"));

        Assert.Single(concurrentOwnerRemovals, result => result.IsSuccess);
        Result<Unit> protectedRemoval = Assert.Single(concurrentOwnerRemovals, result => result.IsFailure);
        Assert.Equal(AccessControlApplicationErrors.LastOwnerProtected, protectedRemoval.Error);
    }

    private static Task<HttpResponseMessage> GrantAsync(HttpClient client, string permission) =>
        AssertSuccess(client.PostAsJsonAsync(
            "/api/admin/roles/support/permissions",
            new { permission }));

    private static async Task<HttpResponseMessage> AssertSuccess(Task<HttpResponseMessage> responseTask)
    {
        HttpResponseMessage response = await responseTask.ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string authenticate = string.Join(", ", response.Headers.WwwAuthenticate.Select(item => item.ToString()));
        Assert.True(
            response.IsSuccessStatusCode,
            $"Status={(int)response.StatusCode} {response.StatusCode}{Environment.NewLine}WWW-Authenticate={authenticate}{Environment.NewLine}{body}");
        return response;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
        return document.RootElement.Clone();
    }
}
