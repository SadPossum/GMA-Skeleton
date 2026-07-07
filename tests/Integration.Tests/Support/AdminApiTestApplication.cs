namespace Integration.Tests.Support;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Administration.Persistence.Entities;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Modules.Auth.Persistence;
using Host.AdminApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NATS.Client.Core;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using Gma.Framework.Persistence.EntityFrameworkCore;

internal sealed class AdminApiTestApplication(
    string provider,
    string providerConnectionString,
    string natsConnectionString,
    bool disableOutboxPublisher = true,
    bool allowGeneratedPasswordResponses = false)
    : WebApplicationFactory<AdminApiAssemblyReference>
{
    private const string JwtIssuer = "GenericModularApi";
    private const string JwtAudience = "GenericModularApi";
    private const string JwtSigningKey = "integration-test-signing-key-change-me-000000000000000000";
    private const string RefreshTokenPepper = "integration-test-refresh-token-pepper-change-me-000000000000000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Integration");
        builder.UseSetting("Persistence:Provider", provider);
        builder.UseSetting("ConnectionStrings:SqlServer", provider == "SqlServer" ? providerConnectionString : string.Empty);
        builder.UseSetting("ConnectionStrings:PostgreSql", provider == "PostgreSql" ? providerConnectionString : string.Empty);
        builder.UseSetting("ConnectionStrings:nats", natsConnectionString);
        builder.UseSetting("NatsJetStream:Enabled", disableOutboxPublisher ? "false" : "true");
        builder.UseSetting("Tenancy:Enabled", "true");
        builder.UseSetting("Outbox:PollIntervalMilliseconds", "100");
        builder.UseSetting("Outbox:LockDurationMilliseconds", "1000");
        builder.UseSetting("Auth:RefreshTokenLifetimeDays", "30");
        builder.UseSetting("Auth:RefreshTokens:Pepper", RefreshTokenPepper);
        builder.UseSetting("Auth:Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Auth:Jwt:Audience", JwtAudience);
        builder.UseSetting("Auth:Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Auth:Jwt:AccessTokenLifetimeMinutes", "15");
        builder.UseSetting(
            "Administration:Api:AllowGeneratedPasswordResponses",
            allowGeneratedPasswordResponses.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Caching:Enabled", "false");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Persistence:Provider"] = provider,
                ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? providerConnectionString : string.Empty,
                ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? providerConnectionString : string.Empty,
                ["ConnectionStrings:nats"] = natsConnectionString,
                ["NatsJetStream:Enabled"] = disableOutboxPublisher ? "false" : "true",
                ["Tenancy:Enabled"] = "true",
                ["Outbox:PollIntervalMilliseconds"] = "100",
                ["Outbox:LockDurationMilliseconds"] = "1000",
                ["Auth:RefreshTokenLifetimeDays"] = "30",
                ["Auth:RefreshTokens:Pepper"] = RefreshTokenPepper,
                ["Auth:Jwt:Issuer"] = JwtIssuer,
                ["Auth:Jwt:Audience"] = JwtAudience,
                ["Auth:Jwt:SigningKey"] = JwtSigningKey,
                ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "15",
                ["Administration:Api:AllowGeneratedPasswordResponses"] = allowGeneratedPasswordResponses.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Caching:Enabled"] = "false"
            };

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            if (disableOutboxPublisher)
            {
                ServiceDescriptor[] hostedServicesToRemove = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType?.Name == "OutboxPublisherService")
                    .ToArray();

                foreach (ServiceDescriptor hostedService in hostedServicesToRemove)
                {
                    services.Remove(hostedService);
                }
            }

            services.RemoveAll<INatsConnection>();
            services.AddSingleton<INatsConnection>(_ => new NatsConnection(new NatsOpts
            {
                Url = natsConnectionString,
            }));
        });
    }

    public async Task MigrateAsync()
    {
        IConfiguration configuration = this.CreatePersistenceConfiguration();

        DbContextOptionsBuilder<AdminDbContext> adminOptions = new();
        adminOptions.UseConfiguredProvider(
            configuration,
            AdminMigrations.SqlServerAssembly,
            AdminMigrations.PostgreSqlAssembly,
            AdminMigrations.Schema,
            AdminMigrations.HistoryTable);
        await using AdminDbContext adminDbContext = new(adminOptions.Options);
        await adminDbContext.Database.MigrateAsync().ConfigureAwait(false);

        DbContextOptionsBuilder<AuthDbContext> authOptions = new();
        authOptions.UseConfiguredProvider(
            configuration,
            AuthMigrations.SqlServerAssembly,
            AuthMigrations.PostgreSqlAssembly,
            AuthMigrations.Schema,
            AuthMigrations.HistoryTable);
        await using AuthDbContext authDbContext = new(authOptions.Options, DisabledTenantContext.Instance);
        await authDbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task SeedOwnerAsync(Guid actorId)
    {
        IConfiguration configuration = this.CreatePersistenceConfiguration();
        DbContextOptionsBuilder<AdminDbContext> options = new();
        options.UseConfiguredProvider(
            configuration,
            AdminMigrations.SqlServerAssembly,
            AdminMigrations.PostgreSqlAssembly,
            AdminMigrations.Schema,
            AdminMigrations.HistoryTable);
        await using AdminDbContext dbContext = new(options.Options);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        AdminRole owner = new(Guid.NewGuid(), "owner", nowUtc);
        string principalId = actorId.ToString();

        dbContext.Principals.Add(new AdminPrincipal(principalId, nowUtc));
        dbContext.Roles.Add(owner);
        dbContext.RolePermissions.Add(new AdminRolePermission(Guid.NewGuid(), owner.Id, "*", nowUtc));
        dbContext.PrincipalRoles.Add(new AdminPrincipalRole(Guid.NewGuid(), principalId, owner.Id, string.Empty, nowUtc));
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<int> CountAuditEntriesAsync(string operation, string? errorCode = null)
    {
        IConfiguration configuration = this.CreatePersistenceConfiguration();
        DbContextOptionsBuilder<AdminDbContext> options = new();
        options.UseConfiguredProvider(
            configuration,
            AdminMigrations.SqlServerAssembly,
            AdminMigrations.PostgreSqlAssembly,
            AdminMigrations.Schema,
            AdminMigrations.HistoryTable);
        await using AdminDbContext dbContext = new(options.Options);

        IQueryable<AdminAuditEntry> query = dbContext.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.Operation == operation);

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            query = query.Where(entry => entry.ErrorCode == errorCode);
        }

        return await query.CountAsync().ConfigureAwait(false);
    }

    public async Task<int> CountAuditEntriesContainingAsync(string value)
    {
        IConfiguration configuration = this.CreatePersistenceConfiguration();
        DbContextOptionsBuilder<AdminDbContext> options = new();
        options.UseConfiguredProvider(
            configuration,
            AdminMigrations.SqlServerAssembly,
            AdminMigrations.PostgreSqlAssembly,
            AdminMigrations.Schema,
            AdminMigrations.HistoryTable);
        await using AdminDbContext dbContext = new(options.Options);

        return await dbContext.AuditEntries
            .AsNoTracking()
            .CountAsync(entry =>
                entry.ActorId.Contains(value) ||
                (entry.TenantId != null && entry.TenantId.Contains(value)) ||
                entry.Operation.Contains(value) ||
                entry.Permission.Contains(value) ||
                entry.Result.Contains(value) ||
                (entry.ErrorCode != null && entry.ErrorCode.Contains(value)))
            .ConfigureAwait(false);
    }

    public string CreateAccessToken(Guid actorId, string tenantId)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITokenService tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        return tokenService.GenerateAccessToken(
            new MemberId(actorId),
            tenantId,
            new MemberSessionId(Guid.NewGuid()));
    }

    public static string CreateAccessTokenWithoutTenantClaim(Guid actorId)
    {
        return CreateJwt(actorId, tenantId: null);
    }

    public static string CreateAccessTokenWithTenantClaim(Guid actorId, string tenantId)
    {
        return CreateJwt(actorId, tenantId);
    }

    public static string CreateAccessTokenWithActorClaim(string actorId, string? tenantId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, actorId)
        ];

        if (tenantId is not null)
        {
            claims.Add(new Claim(GmaClaimNames.TenantId, tenantId));
        }

        JwtSecurityToken token = new(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: nowUtc.AddMinutes(15).UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateJwt(Guid actorId, string? tenantId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, actorId.ToString())
        ];

        if (tenantId is not null)
        {
            claims.Add(new Claim(GmaClaimNames.TenantId, tenantId));
        }

        JwtSecurityToken token = new(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: nowUtc.AddMinutes(15).UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private IConfiguration CreatePersistenceConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = provider,
                ["ConnectionStrings:SqlServer"] = provider == "SqlServer" ? providerConnectionString : string.Empty,
                ["ConnectionStrings:PostgreSql"] = provider == "PostgreSql" ? providerConnectionString : string.Empty,
            })
            .Build();

    private sealed class DisabledTenantContext : ITenantContext
    {
        public static readonly DisabledTenantContext Instance = new();

        public bool IsEnabled => false;
        public string? TenantId => null;
    }
}
