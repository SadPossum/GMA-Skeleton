namespace Integration.Tests;

using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.Administration.Admin.Contracts;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using Integration.Tests.Support;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class AdminCliIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Admin_cli_bootstraps_rbac_and_manages_auth_members_against_sql_server_and_postgre_sql()
    {
        await RunAsync(
            "SqlServer",
            async () =>
            {
                MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                await sqlServer.StartAsync();
                return new ProviderLease(
                    sqlServer,
                    AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_admin_tests"));
            });

        await RunAsync(
            "PostgreSql",
            async () =>
            {
                PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
                    .WithDatabase("gma_admin_tests")
                    .Build();
                await postgreSql.StartAsync();
                return new ProviderLease(postgreSql, postgreSql.GetConnectionString());
            });
    }

    private static async Task RunAsync(string provider, Func<Task<ProviderLease>> createProvider)
    {
        await using ProviderLease providerLease = await createProvider().ConfigureAwait(false);
        await using AdminCliTestApplication application = new(provider, providerLease.ConnectionString);

        await application.MigrateAsync().ConfigureAwait(false);

        await AssertSuccess(application.ExecuteAsync("admin", "bootstrap", "--actor", "owner", "--yes"));
        AdminCliResult invalidRole = await application.ExecuteAsync(
            "admin", "roles", "create",
            "--actor", "owner",
            "--name", "support team");
        Assert.Equal(AdminExitCodes.Failed, invalidRole.ExitCode);
        Assert.Contains(AccessControlApplicationErrors.RoleNameInvalid.Message, invalidRole.Error, StringComparison.Ordinal);
        Assert.Equal(
            0,
            await application.CountAuditEntriesContainingAsync(AccessControlApplicationErrors.RoleNameInvalid.Code).ConfigureAwait(false));

        await AssertSuccess(application.ExecuteAsync("admin", "roles", "create", "--actor", "owner", "--name", "support"));
        AdminCliResult invalidPermission = await application.ExecuteAsync(
            "admin", "roles", "grant",
            "--actor", "owner",
            "--role", "support",
            "--permission", "auth");
        Assert.Equal(AdminExitCodes.Failed, invalidPermission.ExitCode);
        Assert.Contains(AccessControlApplicationErrors.PermissionCodeInvalid.Message, invalidPermission.Error, StringComparison.Ordinal);
        Assert.Equal(
            0,
            await application.CountAuditEntriesContainingAsync(AccessControlApplicationErrors.PermissionCodeInvalid.Code).ConfigureAwait(false));

        await GrantAsync(application, AuthAdminPermissionCodes.MembersRead);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersCreate);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersDisable);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersEnable);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersResetMultiFactor);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersResetPassword);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersRevokeSessions);
        await GrantAsync(application, AdministrationAdminPermissions.AuditRead.Code);
        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-actor", "support",
            "--role", "support",
            "--scope", "tenant:tenant-admin"));

        AdminCliResult tenantScopedAuditDenied = await application.ExecuteAsync(
            "administration", "audit", "list",
            "--actor", "support",
            "--tenant", "tenant-admin");
        Assert.Equal(AdminExitCodes.Unauthorized, tenantScopedAuditDenied.ExitCode);

        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-actor", "support",
            "--role", "support"));
        AdminCliResult auditList = await AssertSuccess(application.ExecuteAsync(
            "administration", "audit", "list",
            "--actor", "support",
            "--operation", AdministrationAdminOperationNames.AuditList,
            "--limit", "1",
            "--output", "json"));
        Assert.Contains(AdministrationAdminOperationNames.AuditList, auditList.Output, StringComparison.Ordinal);
        await AssertSuccess(application.ExecuteAsync(
            "administration", "audit", "list",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--limit", "1"));

        AdminCliResult purgeDenied = await application.ExecuteAsync(
            "administration", "audit", "purge",
            "--actor", "support",
            "--before", "2020-01-01T00:00:00Z",
            "--yes");
        Assert.Equal(AdminExitCodes.Unauthorized, purgeDenied.ExitCode);

        await GrantAsync(application, AdministrationAdminPermissions.AuditPurge.Code);
        AdminCliResult unconfirmedPurge = await application.ExecuteAsync(
            "administration", "audit", "purge",
            "--actor", "support",
            "--before", "2020-01-01T00:00:00Z");
        Assert.Equal(AdminExitCodes.Failed, unconfirmedPurge.ExitCode);
        Assert.Contains(AdminErrors.ConfirmationRequired.Message, unconfirmedPurge.Error, StringComparison.Ordinal);
        AdminCliResult purge = await AssertSuccess(application.ExecuteAsync(
            "administration", "audit", "purge",
            "--actor", "support",
            "--before", "2020-01-01T00:00:00Z",
            "--batch-size", "10",
            "--yes"));
        Assert.Contains("Deleted 0 audit record(s).", purge.Output, StringComparison.Ordinal);

        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "create",
            "--actor", "owner",
            "--name", "property-reader"));
        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "grant",
            "--actor", "owner",
            "--role", "property-reader",
            "--permission", "properties.read"));
        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", "product-user",
            "--role", "property-reader",
            "--scope", "tenant:tenant-admin"));

        AdminCliResult assignments = await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "assignments",
            "--actor", "owner",
            "--role", "property-reader",
            "--output", "json"));
        Assert.Contains("product-user", assignments.Output, StringComparison.Ordinal);
        Assert.Contains("user", assignments.Output, StringComparison.Ordinal);
        Assert.Contains("tenant:tenant-admin", assignments.Output, StringComparison.Ordinal);

        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "unassign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", "product-user",
            "--role", "property-reader",
            "--scope", "tenant:tenant-admin"));
        AdminCliResult missingAssignment = await application.ExecuteAsync(
            "admin", "roles", "unassign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", "product-user",
            "--role", "property-reader",
            "--scope", "tenant:tenant-admin");
        Assert.Equal(AdminExitCodes.Failed, missingAssignment.ExitCode);
        Assert.Contains(AccessControlApplicationErrors.AssignmentNotFound.Message, missingAssignment.Error, StringComparison.Ordinal);

        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "revoke",
            "--actor", "owner",
            "--role", "property-reader",
            "--permission", "properties.read"));
        AdminCliResult missingPermission = await application.ExecuteAsync(
            "admin", "roles", "revoke",
            "--actor", "owner",
            "--role", "property-reader",
            "--permission", "properties.read");
        Assert.Equal(AdminExitCodes.Failed, missingPermission.ExitCode);
        Assert.Contains(AccessControlApplicationErrors.PermissionNotGranted.Message, missingPermission.Error, StringComparison.Ordinal);

        AdminCliResult protectedOwner = await application.ExecuteAsync(
            "admin", "roles", "unassign",
            "--actor", "owner",
            "--target-kind", "admin-actor",
            "--target-id", "owner",
            "--role", "owner");
        Assert.Equal(AdminExitCodes.Failed, protectedOwner.ExitCode);
        Assert.Contains(AccessControlApplicationErrors.LastOwnerProtected.Message, protectedOwner.Error, StringComparison.Ordinal);

        AdminCliResult denied = await application.ExecuteAsync(
            "auth", "members", "list",
            "--actor", "stranger",
            "--tenant", "tenant-admin");
        Assert.Equal(AdminExitCodes.Unauthorized, denied.ExitCode);

        AdminCliResult crossTenantDenied = await application.ExecuteAsync(
            "auth", "members", "create",
            "--actor", "support",
            "--tenant", "tenant-other",
            "--username", $"{provider.ToLowerInvariant()}-other@example.com",
            "--generate-password");
        Assert.Equal(AdminExitCodes.Unauthorized, crossTenantDenied.ExitCode);

        AdminCliResult invalidUsernameType = await application.ExecuteAsync(
            "auth", "members", "create",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--username", $"{provider.ToLowerInvariant()}-invalid-type@example.com",
            "--username-type", "telegram",
            "--generate-password");
        Assert.Equal(AdminExitCodes.Failed, invalidUsernameType.ExitCode);
        Assert.Contains(AuthApplicationErrors.UsernameTypeInvalid.Message, invalidUsernameType.Error, StringComparison.Ordinal);
        Assert.Equal(
            1,
            await application.CountAuditEntriesContainingAsync(AuthApplicationErrors.UsernameTypeInvalid.Code).ConfigureAwait(false));

        AdminCliResult created = await AssertSuccess(application.ExecuteAsync(
            "auth", "members", "create",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--username", $"{provider.ToLowerInvariant()}-member@example.com",
            "--username-type", "email",
            "--generate-password"));
        Guid memberId = created.GetCreatedMemberId();
        string password = created.GetGeneratedPassword();

        AdminCliResult passwordSourceConflict = await application.ExecuteAsync(
            "auth", "members", "create",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--username", $"{provider.ToLowerInvariant()}-conflict@example.com",
            "--generate-password",
            "--password-stdin");
        Assert.Equal(AdminExitCodes.Failed, passwordSourceConflict.ExitCode);
        Assert.Equal(1, await application.CountAuditEntriesContainingAsync("Admin.PasswordSourceConflict").ConfigureAwait(false));

        var loginBeforeDisable = await application
            .LoginAsync("tenant-admin", $"{provider.ToLowerInvariant()}-member@example.com", password)
            .ConfigureAwait(false);
        Assert.True(loginBeforeDisable.IsSuccess);
        Assert.True(await application.CountActiveSessionsAsync(memberId).ConfigureAwait(false) > 0);
        await application.SeedActiveTotpAuthenticatorAsync(memberId, "global").ConfigureAwait(false);

        int resetConfirmationAuditCountBefore = await application
            .CountAuditEntriesContainingAsync(AdminErrors.ConfirmationRequired.Code)
            .ConfigureAwait(false);
        AdminCliResult missingResetConfirmation = await application.ExecuteAsync(
            "auth", "members", "reset-multi-factor",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--member-id", memberId.ToString(),
            "--reason", "verified account recovery");
        Assert.Equal(AdminExitCodes.Failed, missingResetConfirmation.ExitCode);
        Assert.Equal(
            resetConfirmationAuditCountBefore + 1,
            await application.CountAuditEntriesContainingAsync(AdminErrors.ConfirmationRequired.Code).ConfigureAwait(false));

        const string resetReason = "verified account recovery";
        await AssertSuccess(application.ExecuteAsync(
            "auth", "members", "reset-multi-factor",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--member-id", memberId.ToString(),
            "--reason", resetReason,
            "--yes"));
        Assert.Equal(0, await application.CountActiveSessionsAsync(memberId).ConfigureAwait(false));
        Assert.Equal(
            1,
            await application
                .CountMultiFactorResetEventsAsync(memberId, "global", resetReason)
                .ConfigureAwait(false));

        await AssertSuccess(application.ExecuteAsync(
            "auth", "members", "list",
            "--actor", "support",
            "--tenant", "tenant-admin"));
        int disableConfirmationAuditCountBefore = await application
            .CountAuditEntriesContainingAsync(AdminErrors.ConfirmationRequired.Code)
            .ConfigureAwait(false);
        AdminCliResult missingDisableConfirmation = await application.ExecuteAsync(
            "auth", "members", "disable",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--member-id", memberId.ToString(),
            "--reason", "support request");
        Assert.Equal(AdminExitCodes.Failed, missingDisableConfirmation.ExitCode);
        Assert.Equal(
            disableConfirmationAuditCountBefore + 1,
            await application.CountAuditEntriesContainingAsync(AdminErrors.ConfirmationRequired.Code).ConfigureAwait(false));

        await AssertSuccess(application.ExecuteAsync(
            "auth", "members", "disable",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--member-id", memberId.ToString(),
            "--reason", "support request",
            "--yes"));

        var loginAfterDisable = await application
            .LoginAsync("tenant-admin", $"{provider.ToLowerInvariant()}-member@example.com", password)
            .ConfigureAwait(false);
        Assert.True(loginAfterDisable.IsFailure);
        Assert.Equal(AuthDomainErrors.MemberDisabled, loginAfterDisable.Error);

        Assert.Equal(0, await application.CountAuditEntriesContainingAsync(password).ConfigureAwait(false));
    }

    private static Task<AdminCliResult> GrantAsync(AdminCliTestApplication application, string permission) =>
        AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "grant",
            "--actor", "owner",
            "--role", "support",
            "--permission", permission));

    private static async Task<AdminCliResult> AssertSuccess(Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask.ConfigureAwait(false);
        Assert.True(
            result.ExitCode == AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}Output:{Environment.NewLine}{result.Output}{Environment.NewLine}Error:{Environment.NewLine}{result.Error}");
        return result;
    }
}
