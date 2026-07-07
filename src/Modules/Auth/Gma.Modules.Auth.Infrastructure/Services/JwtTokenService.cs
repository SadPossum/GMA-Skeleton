namespace Gma.Modules.Auth.Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Gma.Framework.Security;
using Gma.Framework.Runtime.Time;

internal sealed class JwtTokenService(IOptions<JwtSettings> options, ISystemClock clock) : ITokenService
{
    public string GenerateAccessToken(MemberId memberId, string tenantId, MemberSessionId sessionId)
    {
        AccessTokenClaims accessTokenClaims = new(memberId, tenantId, sessionId);
        JwtSettings settings = options.Value;
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(settings.SigningKey));
        SigningCredentials signingCredentials = new(securityKey, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, accessTokenClaims.MemberId.Value.ToString()),
            new Claim(ApplicationClaimNames.TenantId, accessTokenClaims.TenantId),
            new Claim(ApplicationClaimNames.SessionId, accessTokenClaims.SessionId.Value.ToString())
        ];

        DateTimeOffset nowUtc = clock.UtcNow;
        JwtSecurityToken token = new(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: nowUtc.AddMinutes(settings.AccessTokenLifetimeMinutes).UtcDateTime,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public MemberId? GetMemberId(string accessToken, bool validateLifetime)
    {
        AccessTokenClaims? claims = this.GetAccessTokenClaims(accessToken, validateLifetime);

        return claims?.MemberId;
    }

    public AccessTokenClaims? GetAccessTokenClaims(string accessToken, bool validateLifetime)
    {
        TokenValidationParameters parameters = this.CreateValidationParameters(validateLifetime);
        JwtSecurityTokenHandler handler = new();

        try
        {
            ClaimsPrincipal principal = handler.ValidateToken(accessToken, parameters, out _);
            string? memberIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            string? tenantId = principal.FindFirstValue(ApplicationClaimNames.TenantId);
            string? sessionIdValue = principal.FindFirstValue(ApplicationClaimNames.SessionId);

            return Guid.TryParse(memberIdValue, out Guid memberId) &&
                   Guid.TryParse(sessionIdValue, out Guid sessionId)
                ? new AccessTokenClaims(new MemberId(memberId), tenantId!, new MemberSessionId(sessionId))
                : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal TokenValidationParameters CreateValidationParameters(bool validateLifetime)
    {
        return JwtTokenValidationParametersFactory.Create(options.Value, validateLifetime);
    }
}
