namespace Integration.Tests;

using Gma.Modules.AccessControl.Application;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using DotNet.Testcontainers.Containers;
using Integration.Tests.Support;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
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
        await GrantAsync(application, AuthAdminPermissionCodes.MembersResetPassword);
        await GrantAsync(application, AuthAdminPermissionCodes.MembersRevokeSessions);
        await AssertSuccess(application.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-actor", "support",
            "--role", "support",
            "--scope", "tenant:tenant-admin"));

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

        await AssertSuccess(application.ExecuteAsync(
            "auth", "members", "list",
            "--actor", "support",
            "--tenant", "tenant-admin"));
        AdminCliResult missingDisableConfirmation = await application.ExecuteAsync(
            "auth", "members", "disable",
            "--actor", "support",
            "--tenant", "tenant-admin",
            "--member-id", memberId.ToString(),
            "--reason", "support request");
        Assert.Equal(AdminExitCodes.Failed, missingDisableConfirmation.ExitCode);
        Assert.Equal(1, await application.CountAuditEntriesContainingAsync(AdminErrors.ConfirmationRequired.Code).ConfigureAwait(false));

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
