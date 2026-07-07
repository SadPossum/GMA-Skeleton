namespace Gma.Modules.Auth.Domain.Entities;

using Gma.Framework.Naming;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class MemberSession : TenantEntity<MemberSessionId>
{
    public const int RefreshTokenHashMaxLength = 512;

    private MemberSession() { }

    private MemberSession(
        MemberSessionId id,
        MemberId memberId,
        string tenantId,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset loginDateTimeUtc)
        : base(id, tenantId)
    {
        this.MemberId = memberId;
        this.RefreshTokenHash = NormalizeRefreshTokenHash(refreshTokenHash);
        this.RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
        this.LoginDateTimeUtc = loginDateTimeUtc;
        this.IsActive = true;
    }

    public MemberId MemberId { get; private set; }
    public string RefreshTokenHash { get; private set; } = string.Empty;
    public DateTimeOffset RefreshTokenExpiresAtUtc { get; private set; }
    public DateTimeOffset LoginDateTimeUtc { get; private set; }
    public DateTimeOffset? SignOutDateTimeUtc { get; private set; }
    public bool IsActive { get; private set; }

    internal static Result<MemberSession> Create(
        MemberSessionId id,
        MemberId memberId,
        string tenantId,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset loginDateTimeUtc)
    {
        if (id.Value == Guid.Empty)
        {
            return Result.Failure<MemberSession>(AuthDomainErrors.SessionIdRequired);
        }

        if (memberId.Value == Guid.Empty)
        {
            return Result.Failure<MemberSession>(AuthDomainErrors.MemberIdRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out _))
        {
            return Result.Failure<MemberSession>(AuthDomainErrors.TenantInvalid);
        }

        if (!TryNormalizeRefreshTokenHash(refreshTokenHash, out _))
        {
            return Result.Failure<MemberSession>(AuthDomainErrors.RefreshTokenHashNotValid);
        }

        return Result.Success(new MemberSession(
            id,
            memberId,
            tenantId,
            refreshTokenHash,
            refreshTokenExpiresAtUtc,
            loginDateTimeUtc));
    }

    internal Result Refresh(
        string refreshTokenHash,
        string newRefreshTokenHash,
        DateTimeOffset newRefreshTokenExpiresAtUtc,
        DateTimeOffset nowUtc)
    {
        if (!this.IsActive)
        {
            return Result.Failure(AuthDomainErrors.SessionInactive);
        }

        if (!this.HasRefreshTokenHash(refreshTokenHash))
        {
            return Result.Failure(AuthDomainErrors.RefreshTokenInvalid);
        }

        if (this.RefreshTokenExpiresAtUtc <= nowUtc)
        {
            return Result.Failure(AuthDomainErrors.RefreshTokenExpired);
        }

        if (!TryNormalizeRefreshTokenHash(newRefreshTokenHash, out string? normalizedRefreshTokenHash))
        {
            return Result.Failure(AuthDomainErrors.RefreshTokenHashNotValid);
        }

        this.RefreshTokenHash = normalizedRefreshTokenHash;
        this.RefreshTokenExpiresAtUtc = newRefreshTokenExpiresAtUtc;

        return Result.Success();
    }

    internal bool HasRefreshTokenHash(string refreshTokenHash) =>
        this.IsActive &&
        TryNormalizeRefreshTokenHash(refreshTokenHash, out string? normalizedRefreshTokenHash) &&
        this.RefreshTokenHash == normalizedRefreshTokenHash;

    internal Result SignOut(DateTimeOffset nowUtc)
    {
        if (!this.IsActive)
        {
            return Result.Failure(AuthDomainErrors.SessionInactive);
        }

        this.IsActive = false;
        this.SignOutDateTimeUtc = nowUtc;
        return Result.Success();
    }

    private static string NormalizeRefreshTokenHash(string refreshTokenHash) =>
        refreshTokenHash.Trim();

    private static bool TryNormalizeRefreshTokenHash(
        string? refreshTokenHash,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? normalizedRefreshTokenHash)
    {
        normalizedRefreshTokenHash = null;

        if (string.IsNullOrWhiteSpace(refreshTokenHash))
        {
            return false;
        }

        string candidate = refreshTokenHash.Trim();
        if (candidate.Length > RefreshTokenHashMaxLength)
        {
            return false;
        }

        normalizedRefreshTokenHash = candidate;
        return true;
    }
}
