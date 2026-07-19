namespace Integration.Tests.Support;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Gma.Framework.Security;
using Gma.Modules.AccessControl.Application.Commands;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Administration.Persistence.Entities;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Application.Ports;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
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

internal sealed class AdminApiTestApplication(
    string provider,
    string providerConnectionString,
    string natsConnectionString,
    bool disableOutboxPublisher = true,
    bool allowGeneratedPasswordResponses = false)
    : WebApplicationFactory<AdminApiAssemblyReference>
{
    private const string JwtIssuer = "GMA-Skeleton";
    private const string JwtAudience = "GMA-Skeleton";
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
        builder.UseSetting("Http:PrivateNetwork:Enabled", "false");
        builder.UseSetting("Http:RateLimiting:Enabled", "false");

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
                ["Caching:Enabled"] = "false",
                ["Http:PrivateNetwork:Enabled"] = "false",
                ["Http:RateLimiting:Enabled"] = "false"
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

        DbContextOptionsBuilder<AccessControlDbContext> accessControlOptions = new();
        accessControlOptions.UseConfiguredProvider(
            configuration,
            AccessControlMigrations.SqlServerAssembly,
            AccessControlMigrations.PostgreSqlAssembly,
            AccessControlMigrations.Schema,
            AccessControlMigrations.HistoryTable);
        await using AccessControlDbContext accessControlDbContext = new(accessControlOptions.Options);
        await accessControlDbContext.Database.MigrateAsync().ConfigureAwait(false);

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
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        var result = await dispatcher
            .SendAsync(new BootstrapOwnerCommand(actorId.ToString(), Confirmed: true), CancellationToken.None)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }
    }

    public async Task<Result<Unit>> UnassignGlobalAdminOwnerAsync(Guid actorId, string roleName)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        return await dispatcher.SendAsync(
            new UnassignRoleCommand(
                AccessSubjectKind.AdminActor,
                actorId.ToString(),
                roleName,
                AccessScope.Global),
            CancellationToken.None).ConfigureAwait(false);
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(string scopeId, string username, string password)
    {
        using IServiceScope scope = this.Services.CreateScope();
        Gma.Framework.Tenancy.ITenantContextAccessor tenantContext = scope.ServiceProvider
            .GetRequiredService<Gma.Framework.Tenancy.ITenantContextAccessor>();
        tenantContext.SetTenant(scopeId);
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<PrimaryAuthenticationResult> result = await dispatcher
            .SendAsync(new LoginMemberCommand(username, password), CancellationToken.None)
            .ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(result.Error);
        }

        return result.Value.Tokens is { } tokens
            ? Result.Success(tokens)
            : Result.Failure<AuthTokensResponse>(AuthApplicationErrors.MultiFactorChallengeInvalid);
    }

    public async Task SeedActiveTotpAuthenticatorAsync(Guid memberId, string scopeId)
    {
        await using AuthDbContext dbContext = this.CreateAuthDbContext();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        Result<MemberTotpAuthenticator> created = MemberTotpAuthenticator.BeginEnrollment(
            new MemberTotpAuthenticatorId(Guid.NewGuid()),
            new MemberId(memberId),
            scopeId,
            "integration-test-protected-secret",
            nowUtc.AddMinutes(10),
            nowUtc);
        Xunit.Assert.True(created.IsSuccess, created.Error.Message);
        Result activated = created.Value.Activate(
            acceptedTimeStep: 1,
            [new TotpRecoveryCodeRegistration(new MemberTotpRecoveryCodeId(Guid.NewGuid()), "integration-test-recovery-hash")],
            nowUtc);
        Xunit.Assert.True(activated.IsSuccess, activated.Error.Message);

        dbContext.MemberTotpAuthenticators.Add(created.Value);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<int> CountActiveSessionsAsync(Guid memberId)
    {
        await using AuthDbContext dbContext = this.CreateAuthDbContext();
        return await dbContext.MemberSessions
            .CountAsync(session => session.MemberId == new MemberId(memberId) && session.IsActive)
            .ConfigureAwait(false);
    }

    public async Task<int> CountMultiFactorResetEventsAsync(Guid memberId, string scopeId, string reason)
    {
        await using AuthDbContext dbContext = this.CreateAuthDbContext();
        string memberIdText = memberId.ToString();
        string eventType = typeof(MemberMultiFactorAuthenticationResetIntegrationEvent).FullName!;
        return await dbContext.OutboxMessages
            .CountAsync(message =>
                message.EventType == eventType &&
                message.ScopeId == scopeId &&
                message.Payload.Contains(memberIdText) &&
                message.Payload.Contains(reason))
            .ConfigureAwait(false);
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

    public string CreateAccessToken(Guid actorId, string scopeId)
    {
        using IServiceScope scope = this.Services.CreateScope();
        ITokenService tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        return tokenService.GenerateAccessToken(new AccessTokenClaims(
            new MemberId(actorId),
            scopeId,
            new MemberSessionId(Guid.NewGuid()),
            SessionAuthenticationEvidence.Password(DateTimeOffset.UtcNow)));
    }

    public static string CreateAccessTokenWithoutTenantClaim(Guid actorId)
    {
        return CreateJwt(actorId, scopeId: null);
    }

    public static string CreateAccessTokenWithTenantClaim(Guid actorId, string scopeId)
    {
        return CreateJwt(actorId, scopeId);
    }

    public static string CreateAccessTokenWithActorClaim(string actorId, string? scopeId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, actorId)
        ];

        if (scopeId is not null)
        {
            claims.Add(new Claim(GmaClaimNames.ScopeId, scopeId));
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

    private static string CreateJwt(Guid actorId, string? scopeId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, actorId.ToString())
        ];

        if (scopeId is not null)
        {
            claims.Add(new Claim(GmaClaimNames.ScopeId, scopeId));
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

    private AuthDbContext CreateAuthDbContext()
    {
        DbContextOptionsBuilder<AuthDbContext> options = new();
        options.UseConfiguredProvider(
            this.CreatePersistenceConfiguration(),
            AuthMigrations.SqlServerAssembly,
            AuthMigrations.PostgreSqlAssembly,
            AuthMigrations.Schema,
            AuthMigrations.HistoryTable);
        return new AuthDbContext(options.Options, DisabledTenantContext.Instance);
    }

    private sealed class DisabledTenantContext : IAuthScopeContext
    {
        public static readonly DisabledTenantContext Instance = new();

        public bool IsEnabled => false;
        public string? ScopeId => null;
        public bool TryRestoreScope(string? scopeId) => true;
    }
}
