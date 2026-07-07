namespace Integration.Tests.Support;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gma.Modules.Files.Api;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Security;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.FileManagement.LocalStorage;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Security;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Modules.Tenancy.Api;

internal sealed class FilesApiTestApplication : IAsyncDisposable
{
    private const string JwtIssuer = "GenericModularApi";
    private const string JwtAudience = "GenericModularApi";
    private const string JwtSigningKey = "files-api-test-signing-key-change-me-000000000000000000";

    private readonly WebApplication app;
    private readonly string storageRoot;

    private FilesApiTestApplication(WebApplication app, string storageRoot)
    {
        this.app = app;
        this.storageRoot = storageRoot;
    }

    public static async Task<FilesApiTestApplication> CreateAsync(bool tenancyEnabled = true)
    {
        string storageRoot = Path.Combine(Path.GetTempPath(), $"gma-files-api-{Guid.NewGuid():N}");
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Integration"
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationIdentity:Namespace"] = "test-app",
            ["Tenancy:Enabled"] = tenancyEnabled.ToString(),
            ["Tenancy:LocalDefaultTenantId"] = "default",
            ["FileManagement:Enabled"] = "true",
            ["FileManagement:Provider"] = "LocalStorage",
            ["FileManagement:MaximumObjectBytes"] = "1048576",
            ["FileManagement:AllowedContentTypes:0"] = "text/plain",
            ["FileManagement:LocalStorage:RootPath"] = storageRoot
        });

        builder.AddTenancyInfrastructure();
        builder.AddRuntimeInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.AddLocalFileStorage();
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

        if (tenancyEnabled)
        {
            builder.AddModule<TenancyModule>();
        }

        builder.AddModule<FilesModule>();
        builder.ValidateModuleComposition();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapModules();

        await app.StartAsync().ConfigureAwait(false);
        return new FilesApiTestApplication(app, storageRoot);
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

    public async ValueTask DisposeAsync()
    {
        await this.app.DisposeAsync().ConfigureAwait(false);
        if (Directory.Exists(this.storageRoot))
        {
            Directory.Delete(this.storageRoot, recursive: true);
        }
    }
}
