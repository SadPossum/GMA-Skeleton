namespace Integration.Tests.Support;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Host.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Gma.Framework.Notifications;
using Gma.Framework.Realtime.Notifications;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;

internal sealed class NotificationStreamingTestApplication(bool tenancyEnabled = true)
    : WebApplicationFactory<ApiAssemblyReference>
{
    private const string JwtIssuer = "GMA-Skeleton";
    private const string JwtAudience = "GMA-Skeleton";
    private const string JwtSigningKey = "notification-streaming-test-signing-key-change-me-0000000000";
    private const string RefreshTokenPepper = "notification-streaming-test-refresh-token-pepper-change-me-000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string tenancyEnabledValue = tenancyEnabled.ToString();

        builder.UseEnvironment("Integration");
        builder.UseSetting("ApplicationIdentity:Namespace", "test-app");
        builder.UseSetting("Persistence:Provider", "SqlServer");
        builder.UseSetting("ConnectionStrings:SqlServer", "Server=localhost,1433;Database=unused;User Id=sa;Password=Pass@word1;TrustServerCertificate=True");
        builder.UseSetting("ConnectionStrings:PostgreSql", string.Empty);
        builder.UseSetting("ConnectionStrings:nats", string.Empty);
        builder.UseSetting("NatsJetStream:Enabled", "false");
        builder.UseSetting("Tenancy:Enabled", tenancyEnabledValue);
        builder.UseSetting("Notifications:Enabled", "true");
        builder.UseSetting("Notifications:Sse:Enabled", "true");
        builder.UseSetting("Notifications:Sse:HeartbeatInterval", "00:00:01");
        builder.UseSetting("Notifications:SignalR:Enabled", "true");
        builder.UseSetting("Caching:Enabled", "false");
        builder.UseSetting("Auth:RefreshTokens:Pepper", RefreshTokenPepper);
        builder.UseSetting("Auth:Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Auth:Jwt:Audience", JwtAudience);
        builder.UseSetting("Auth:Jwt:SigningKey", JwtSigningKey);
        builder.UseSetting("Auth:Jwt:AccessTokenLifetimeMinutes", "15");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApplicationIdentity:Namespace"] = "test-app",
                ["Persistence:Provider"] = "SqlServer",
                ["ConnectionStrings:SqlServer"] = "Server=localhost,1433;Database=unused;User Id=sa;Password=Pass@word1;TrustServerCertificate=True",
                ["ConnectionStrings:PostgreSql"] = string.Empty,
                ["ConnectionStrings:nats"] = string.Empty,
                ["NatsJetStream:Enabled"] = "false",
                ["Tenancy:Enabled"] = tenancyEnabledValue,
                ["Notifications:Enabled"] = "true",
                ["Notifications:Sse:Enabled"] = "true",
                ["Notifications:Sse:HeartbeatInterval"] = "00:00:01",
                ["Notifications:SignalR:Enabled"] = "true",
                ["Caching:Enabled"] = "false",
                ["Auth:RefreshTokens:Pepper"] = RefreshTokenPepper,
                ["Auth:Jwt:Issuer"] = JwtIssuer,
                ["Auth:Jwt:Audience"] = JwtAudience,
                ["Auth:Jwt:SigningKey"] = JwtSigningKey,
                ["Auth:Jwt:AccessTokenLifetimeMinutes"] = "15",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // This fixture exercises the in-process streaming transports without a database.
            // Durable history and routing policy behavior is covered by the Notifications module tests.
            services.RemoveAll<IUserNotificationHistoryWriter>();
            services.RemoveAll<IUserNotificationDeliveryPolicyEvaluator>();

            if (!tenancyEnabled)
            {
                services.PostConfigure<TenantOptions>(options => options.Enabled = false);
            }
        });
    }

    public static string CreateAccessToken(string? scopeId, string userId)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(JwtSigningKey));
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId),
            new(ApplicationClaimNames.SessionId, Guid.NewGuid().ToString())
        ];
        if (!string.IsNullOrWhiteSpace(scopeId))
        {
            claims.Add(new Claim(ApplicationClaimNames.ScopeId, scopeId));
        }

        JwtSecurityToken token = new(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task PublishAsync(
        string scopeId,
        string userId,
        string title,
        NotificationSeverity severity = NotificationSeverity.Info,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = this.Services.CreateScope();
        IUserNotificationPublisher publisher = scope.ServiceProvider.GetRequiredService<IUserNotificationPublisher>();
        await publisher.PublishAsync(
                "test-module",
                UserNotificationTarget.User(scopeId, userId),
                new StreamingTestNotificationPayload(title),
                new NotificationPublishOptions(title, severity: severity),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [NotificationName(Name)]
    [NotificationVersion(Version)]
    [NotificationDescription("Notification streaming integration-test payload.")]
    private sealed record StreamingTestNotificationPayload(string Title) : IUserNotificationPayload
    {
        public const string Name = "streaming.test";
        public const int Version = 1;
    }
}
