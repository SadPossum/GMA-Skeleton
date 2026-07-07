namespace Gma.Modules.Auth.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Modules.Auth.Infrastructure;
using Gma.Modules.Auth.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Gma.Framework.Security;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Generate_access_token_normalizes_tenant_claim()
    {
        JwtTokenService service = CreateService();
        MemberId memberId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        MemberSessionId sessionId = new(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));

        string token = service.GenerateAccessToken(memberId, " tenant-a ", sessionId);
        AccessTokenClaims? claims = service.GetAccessTokenClaims(token, validateLifetime: false);

        Assert.NotNull(claims);
        Assert.Equal(memberId, claims.MemberId);
        Assert.Equal("tenant-a", claims.TenantId);
        Assert.Equal(sessionId, claims.SessionId);
    }

    [Fact]
    public void Generate_access_token_rejects_invalid_identity()
    {
        JwtTokenService service = CreateService();
        MemberId memberId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        MemberSessionId sessionId = new(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));

        Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(default, "tenant-a", sessionId));
        Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(memberId, "tenant-a", default));
        Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(memberId, " ", sessionId));
    }

    [Fact]
    public void Get_access_token_claims_rejects_unexpected_signing_algorithm()
    {
        JwtSettings settings = CreateSettings();
        JwtTokenService service = CreateService(settings);
        MemberId memberId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        MemberSessionId sessionId = new(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(settings.SigningKey));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha384);
        JwtSecurityToken token = new(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, memberId.Value.ToString()),
                new Claim(GmaClaimNames.TenantId, "tenant-a"),
                new Claim(GmaClaimNames.SessionId, sessionId.Value.ToString())
            ],
            notBefore: new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc),
            expires: new DateTime(2026, 7, 2, 12, 15, 0, DateTimeKind.Utc),
            signingCredentials: signingCredentials);

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        Assert.Null(service.GetAccessTokenClaims(accessToken, validateLifetime: false));
    }

    private static JwtTokenService CreateService(JwtSettings? settings = null) =>
        new(Options.Create(settings ?? CreateSettings()), new FixedClock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero)));

    private static JwtSettings CreateSettings() =>
        new()
        {
            Issuer = "GenericModularApi.Tests",
            Audience = "GenericModularApi.Tests",
            SigningKey = "test-signing-key-with-enough-bytes-00000000000000000000",
            AccessTokenLifetimeMinutes = 15
        };

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
