namespace Integration.Tests.Support;

using Gma.Modules.Administration.AdminCli;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Auth.AdminCli;
using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Tenancy;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Tenancy.Caching;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;

internal sealed class AdminCliTestApplication : IAsyncDisposable
{
    private readonly IHost host;
    private readonly RootCommand rootCommand;

    public AdminCliTestApplication(string provider, string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Environment.EnvironmentName = "Integration";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = provider,
            ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? connectionString : string.Empty,
            ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? connectionString : string.Empty,
            ["Tenancy:Enabled"] = "true",
            ["Administration:Bootstrap:AllowWhenAssignmentsExist"] = "false",
            ["Auth:RefreshTokenLifetimeDays"] = "30",
            ["Auth:RefreshTokens:Pepper"] = "integration-test-refresh-token-pepper-change-me-000000000000000000",
            ["Auth:Jwt:Issuer"] = "GMA-Skeleton",
            ["Auth:Jwt:Audience"] = "GMA-Skeleton",
            ["Auth:Jwt:SigningKey"] = "integration-test-signing-key-change-me-000000000000000000",
            ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "15",
            ["Caching:Enabled"] = "false"
        });

        builder.Services.AddGmaAdministrationCli();
        builder.AddCachingCqrs();
        builder.AddGmaInfrastructure();
        builder.AddTenantCaching();
        builder.AddMessagingInfrastructure();
        builder.AddTenantAwareMessaging();
        builder.AddAdminModule<AdministrationAdminCliModule>();
        builder.AddAdminModule<AuthAdminCliModule>();

        this.host = builder.Build();
        this.host.Services.ValidateAdminCliStartup();
        this.rootCommand = this.host.Services.CreateAdminRootCommand();
    }

    public async Task MigrateAsync()
    {
        using IServiceScope scope = this.host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AdminDbContext>().Database.MigrateAsync().ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task<AdminCliResult> ExecuteAsync(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            ParseResult parseResult = this.rootCommand.Parse(args);
            int exitCode = await parseResult
                .InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false }, CancellationToken.None)
                .ConfigureAwait(false);

            return new AdminCliResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(string tenantId, string username, string password)
    {
        using IServiceScope scope = this.host.Services.CreateScope();
        ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(tenantId);
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.SendAsync(new LoginMemberCommand(username, password), CancellationToken.None)
            .ConfigureAwait(false);
    }

    public async Task<int> CountAuditEntriesContainingAsync(string value)
    {
        using IServiceScope scope = this.host.Services.CreateScope();
        AdminDbContext dbContext = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        return await dbContext.AuditEntries
            .CountAsync(entry =>
                entry.ActorId.Contains(value) ||
                entry.Operation.Contains(value) ||
                entry.Permission.Contains(value) ||
                (entry.ErrorCode != null && entry.ErrorCode.Contains(value)))
            .ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        this.host.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed record AdminCliResult(int ExitCode, string Output, string Error)
{
    public Guid GetCreatedMemberId()
    {
        Match match = Regex.Match(this.Output, @"Created member '(?<id>[0-9a-fA-F-]{36})'");
        Xunit.Assert.True(match.Success, this.Output);
        return Guid.Parse(match.Groups["id"].Value);
    }

    public string GetGeneratedPassword()
    {
        Match match = Regex.Match(this.Output, @"Generated password: (?<password>\S+)");
        Xunit.Assert.True(match.Success, this.Output);
        return match.Groups["password"].Value;
    }
}
