namespace Gma.Modules.Auth.Application;

using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal abstract class AuthCommandHandlerBase(
    ITokenService tokenService,
    IRefreshTokenHashingService refreshTokenHashingService,
    ISystemClock clock,
    IIdGenerator idGenerator)
{
    protected ISystemClock Clock => clock;
    protected IIdGenerator IdGenerator => idGenerator;

    protected (MemberSessionId SessionId, string AccessToken, string RefreshToken, string RefreshTokenHash, DateTimeOffset ExpiresAtUtc)
        CreateTokens(MemberId memberId, string tenantId, TimeSpan refreshTokenLifetime)
    {
        MemberSessionId sessionId = new(this.IdGenerator.NewId());
        string accessToken = tokenService.GenerateAccessToken(memberId, tenantId, sessionId);
        string refreshToken = tokenService.GenerateRefreshToken();
        string refreshTokenHash = refreshTokenHashingService.HashRefreshToken(refreshToken);
        DateTimeOffset expiresAtUtc = this.Clock.UtcNow.Add(refreshTokenLifetime);

        return (sessionId, accessToken, refreshToken, refreshTokenHash, expiresAtUtc);
    }
}
