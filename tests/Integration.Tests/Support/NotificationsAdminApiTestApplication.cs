namespace Integration.Tests.Support;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.AccessControl.Application.Commands;
using Gma.Modules.AccessControl.Persistence;
using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Persistence;
using Gma.Modules.Administration.Persistence.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Gma.Modules.Notifications.AdminApi;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Administration.AccessControl;
using Gma.Framework.Administration.Api;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Api.Security;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Scoping;
using Gma.Framework.Results;
using DomainNotificationSeverity = Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity;

internal sealed class NotificationsAdminApiTestApplication : IAsyncDisposable
{
    private const string JwtIssuer = "GMA-Skeleton";
    private const string JwtAudience = "GMA-Skeleton";
    private const string JwtSigningKey = "notifications-admin-api-test-signing-key-change-me-000000";

    private readonly WebApplication app;

    private NotificationsAdminApiTestApplication(WebApplication app) => this.app = app;

    public static async Task<NotificationsAdminApiTestApplication> CreateAsync()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = $"notifications-admin-api-{Guid.NewGuid():N}";
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Integration"
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationIdentity:Namespace"] = "test-app",
            ["Persistence:Provider"] = "SqlServer",
            ["ConnectionStrings:SqlServer"] = "Server=localhost;Database=notifications-admin-api-tests;Trusted_Connection=True;TrustServerCertificate=True",
            ["Tenancy:Enabled"] = "true",
            ["AccessControl:Bootstrap:OwnerRoleName"] = "owner",
            ["Notifications:DurableStreams:BatchSize"] = "10",
            ["Notifications:DurableStreams:PollInterval"] = "00:00:01",
            ["Caching:Enabled"] = "false"
        });

        builder.AddTenancyInfrastructure();
        builder.AddTenantScoping();
        builder.AddRuntimeInfrastructure();
        builder.AddApplicationEventsInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.Services.AddGmaAdministrationApi(builder.Configuration);
        builder.Services.AddGmaAccessControlAdministrationAuthorization();
        builder.Services.AddAccessControlApplication(builder.Configuration);
        builder.Services.AddAdministrationApplication(builder.Configuration);
        builder.Services.AddApiSecurityDefaults();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddDbContext<AdminDbContext>(
            options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        builder.AddAdministrationPersistence();
        builder.Services.AddDbContext<AccessControlDbContext>(
            options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        builder.AddAccessControlPersistence();
        builder.Services.AddDbContext<NotificationsDbContext>(
            options => options.UseInMemoryDatabase(databaseName, databaseRoot));
        builder.AddAdminApiModule<NotificationsAdminApiModule>();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAdminApiModules();

        await app.StartAsync().ConfigureAwait(false);
        return new NotificationsAdminApiTestApplication(app);
    }

    public HttpClient CreateClient() => this.app.GetTestClient();

    public static string CreateAccessToken(string actorId, string? scopeId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, actorId)
        ];

        if (!string.IsNullOrWhiteSpace(scopeId))
        {
            claims.Add(new Claim(ApplicationClaimNames.ScopeId, scopeId));
        }

        JwtSecurityToken token = new(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: nowUtc.AddMinutes(-1).UtcDateTime,
            expires: nowUtc.AddMinutes(15).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task SeedOwnerAsync(string actorId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = this.app.Services.CreateScope();
        IRequestDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();

        Result<Unit> result = await dispatcher
            .SendAsync(new BootstrapOwnerCommand(actorId, Confirmed: true), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }
    }

    public async Task AddNotificationAsync(
        string scopeId,
        string userId,
        Guid notificationId,
        string title,
        long streamSequence,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = this.app.Services.CreateScope();
        ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(scopeId);
        NotificationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        UserNotification notification = UserNotification.Create(
            notificationId,
            scopeId,
            userId,
            "catalog",
            "catalog.item-updated",
            1,
            title,
            null,
            DomainNotificationSeverity.Info,
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero).AddMinutes(streamSequence),
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero).AddMinutes(streamSequence),
            $$"""{"title":"{{title}}"}""").Value;
        typeof(UserNotification)
            .GetProperty(nameof(UserNotification.StreamSequence))!
            .SetValue(notification, streamSequence);

        dbContext.UserNotifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAuditEntriesAsync(string operation, string? errorCode = null)
    {
        using IServiceScope scope = this.app.Services.CreateScope();
        AdminDbContext dbContext = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        IQueryable<AdminAuditEntry> query = dbContext.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.Operation == operation);

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            query = query.Where(entry => entry.ErrorCode == errorCode);
        }

        return await query.CountAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => this.app.DisposeAsync();
}
