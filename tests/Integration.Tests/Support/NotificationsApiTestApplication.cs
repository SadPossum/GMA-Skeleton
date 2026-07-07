namespace Integration.Tests.Support;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Gma.Modules.Notifications.Api;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Persistence;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Security;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Modules.Tenancy.Api;
using DomainBroadcastAudience = Gma.Modules.Notifications.Domain.ValueObjects.NotificationBroadcastAudience;
using DomainNotificationSeverity = Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity;

internal sealed class NotificationsApiTestApplication : IAsyncDisposable
{
    private const string JwtIssuer = "GenericModularApi";
    private const string JwtAudience = "GenericModularApi";
    private const string JwtSigningKey = "notifications-api-test-signing-key-change-me-000000000000";

    private readonly WebApplication app;

    private NotificationsApiTestApplication(WebApplication app) => this.app = app;

    public static async Task<NotificationsApiTestApplication> CreateAsync(bool tenancyEnabled = true)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = $"notifications-api-{Guid.NewGuid():N}";
        string tenancyEnabledValue = tenancyEnabled.ToString();
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Integration"
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationIdentity:Namespace"] = "test-app",
            ["Persistence:Provider"] = "SqlServer",
            ["ConnectionStrings:SqlServer"] = "Server=localhost;Database=notifications-api-tests;Trusted_Connection=True;TrustServerCertificate=True",
            ["Tenancy:Enabled"] = tenancyEnabledValue,
            ["Tenancy:LocalDefaultTenantId"] = "default",
            ["Notifications:DurableStreams:BatchSize"] = "10",
            ["Notifications:DurableStreams:PollInterval"] = "00:00:01",
            ["Caching:Enabled"] = "false"
        });

        builder.AddTenancyInfrastructure();
        builder.AddRuntimeInfrastructure();
        builder.AddApplicationEventsInfrastructure();
        builder.AddCqrsInfrastructure();
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

        builder.Services.AddDbContext<NotificationsDbContext>(
            options => options.UseInMemoryDatabase(databaseName, databaseRoot));

        if (tenancyEnabled)
        {
            builder.AddModule<TenancyModule>();
        }

        builder.AddModule<NotificationsModule>();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapModules();

        await app.StartAsync().ConfigureAwait(false);
        return new NotificationsApiTestApplication(app);
    }

    public HttpClient CreateClient() => this.app.GetTestClient();

    public static string CreateAccessToken(string tenantId, string userId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId),
            new(ApplicationClaimNames.TenantId, tenantId),
            new(ApplicationClaimNames.SessionId, Guid.NewGuid().ToString())
        ];

        JwtSecurityToken token = new(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: nowUtc.AddMinutes(-1).UtcDateTime,
            expires: nowUtc.AddMinutes(15).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task AddNotificationAsync(
        string tenantId,
        string userId,
        Guid notificationId,
        string title,
        long streamSequence,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = this.app.Services.CreateScope();
        ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        tenantContext.SetTenant(tenantId);
        NotificationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        UserNotification notification = UserNotification.Create(
            notificationId,
            tenantId,
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
        SetStreamSequence(notification, streamSequence);
        dbContext.UserNotifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddBroadcastAsync(
        string? tenantId,
        DomainBroadcastAudience audience,
        Guid broadcastId,
        string title,
        long streamSequence,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = this.app.Services.CreateScope();
        ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            tenantContext.SetTenant(tenantId);
        }

        NotificationsDbContext dbContext = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        NotificationBroadcast broadcast = NotificationBroadcast.Create(
            broadcastId,
            tenantId,
            audience,
            "notifications",
            "system.notice",
            1,
            title,
            null,
            DomainNotificationSeverity.Warning,
            new DateTimeOffset(2026, 7, 5, 13, 0, 0, TimeSpan.Zero).AddMinutes(streamSequence),
            new DateTimeOffset(2026, 7, 5, 13, 0, 0, TimeSpan.Zero).AddMinutes(streamSequence),
            $$"""{"title":"{{title}}"}""").Value;
        SetStreamSequence(broadcast, streamSequence);
        dbContext.NotificationBroadcasts.Add(broadcast);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => this.app.DisposeAsync();

    private static void SetStreamSequence(UserNotification notification, long sequence) =>
        typeof(UserNotification)
            .GetProperty(nameof(UserNotification.StreamSequence))!
            .SetValue(notification, sequence);

    private static void SetStreamSequence(NotificationBroadcast broadcast, long sequence) =>
        typeof(NotificationBroadcast)
            .GetProperty(nameof(NotificationBroadcast.StreamSequence))!
            .SetValue(broadcast, sequence);
}
