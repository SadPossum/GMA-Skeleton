namespace Gma.Modules.Auth.Domain.Aggregates;

using Gma.Framework.Naming;
using Gma.Modules.Auth.Domain.Entities;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Events;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class Member : TenantAggregateRoot<MemberId>
{
    public const int PasswordHashMaxLength = 512;
    public const int DisabledReasonMaxLength = 512;

    private readonly List<MemberSession> sessions = [];
    private readonly List<MemberUsername> usernames = [];

    private Member() { }

    private Member(MemberId id, string tenantId, string passwordHash)
        : base(id, tenantId)
    {
        this.PasswordHash = passwordHash;
        this.Status = MemberStatus.Active;
    }

    public string PasswordHash { get; private set; } = string.Empty;
    public MemberStatus Status { get; private set; } = MemberStatus.Active;
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset? DisabledAtUtc { get; private set; }
    public string? DisabledReason { get; private set; }
    public IReadOnlyCollection<MemberUsername> Usernames => this.usernames;
    public IReadOnlyCollection<MemberSession> Sessions => this.sessions;

    public static Result<Member> Create(
        MemberId id,
        string tenantId,
        string username,
        MemberUsernameType usernameType,
        string passwordHash,
        MemberUsernameId usernameId,
        Guid registeredEventId,
        DateTimeOffset registeredAtUtc)
    {
        if (id.Value == Guid.Empty)
        {
            return Result.Failure<Member>(AuthDomainErrors.MemberIdRequired);
        }

        if (registeredEventId == Guid.Empty)
        {
            return Result.Failure<Member>(AuthDomainErrors.DomainEventIdRequired);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<Member>(AuthDomainErrors.TenantRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
        {
            return Result.Failure<Member>(AuthDomainErrors.TenantInvalid);
        }

        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > PasswordHashMaxLength)
        {
            return Result.Failure<Member>(AuthDomainErrors.PasswordNotValid);
        }

        Member member = new(id, normalizedTenantId, passwordHash)
        {
            RegisteredAtUtc = registeredAtUtc
        };
        Result<MemberUsername> usernameResult = member.AddUsername(usernameId, username, usernameType);

        if (usernameResult.IsFailure)
        {
            return Result.Failure<Member>(usernameResult.Error);
        }

        member.RaiseDomainEvent(new MemberRegisteredDomainEvent(
            registeredEventId,
            registeredAtUtc,
            member.Id,
            member.TenantId,
            usernameResult.Value.Value));

        return Result.Success(member);
    }

    public Result<MemberUsername> AddUsername(
        MemberUsernameId usernameId,
        string value,
        MemberUsernameType usernameType)
    {
        Result<MemberUsername> usernameResult = MemberUsername.Create(
            usernameId,
            this.Id,
            this.TenantId,
            value,
            usernameType);

        if (usernameResult.IsFailure)
        {
            return Result.Failure<MemberUsername>(usernameResult.Error);
        }

        MemberUsername newUsername = usernameResult.Value;
        if (this.usernames.Any(username => username.NormalizedValue == newUsername.NormalizedValue))
        {
            return Result.Failure<MemberUsername>(AuthDomainErrors.UsernameAlreadyExists);
        }

        MemberUsername? current = this.usernames.FirstOrDefault(username =>
            username.UsernameType == usernameType && username.IsActive);

        current?.Deactivate();
        this.usernames.Add(newUsername);

        return Result.Success(newUsername);
    }

    public bool HasActiveUsername(string username) =>
        MemberUsername.TryNormalize(username, out string? normalizedUsername) &&
        this.usernames.Any(memberUsername =>
            memberUsername.IsActive &&
            memberUsername.NormalizedValue == normalizedUsername);

    public Result<MemberSession> StartSession(
        MemberSessionId sessionId,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAtUtc,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureCanAuthenticate();
        if (statusResult.IsFailure)
        {
            return Result.Failure<MemberSession>(statusResult.Error);
        }

        Result<MemberSession> sessionResult = MemberSession.Create(
            sessionId,
            this.Id,
            this.TenantId,
            refreshTokenHash,
            refreshTokenExpiresAtUtc,
            nowUtc);

        if (sessionResult.IsFailure)
        {
            return Result.Failure<MemberSession>(sessionResult.Error);
        }

        MemberSession session = sessionResult.Value;
        this.sessions.Add(session);

        return Result.Success(session);
    }

    public Result<MemberSession> RefreshSession(
        MemberSessionId sessionId,
        string refreshTokenHash,
        string newRefreshTokenHash,
        DateTimeOffset newRefreshTokenExpiresAtUtc,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureCanAuthenticate();
        if (statusResult.IsFailure)
        {
            return Result.Failure<MemberSession>(statusResult.Error);
        }

        MemberSession? session = this.sessions.FirstOrDefault(item =>
            item.Id == sessionId && item.HasRefreshTokenHash(refreshTokenHash));

        if (session is null)
        {
            return Result.Failure<MemberSession>(AuthDomainErrors.SessionNotFound);
        }

        Result result = session.Refresh(refreshTokenHash, newRefreshTokenHash, newRefreshTokenExpiresAtUtc, nowUtc);

        return result.IsSuccess
            ? Result.Success(session)
            : Result.Failure<MemberSession>(result.Error);
    }

    public Result SignOut(string refreshTokenHash, DateTimeOffset nowUtc)
    {
        MemberSession? session = this.sessions.FirstOrDefault(item => item.HasRefreshTokenHash(refreshTokenHash));

        return session is null
            ? Result.Failure(AuthDomainErrors.SessionNotFound)
            : session.SignOut(nowUtc);
    }

    public Result SignOutAll(DateTimeOffset nowUtc)
    {
        List<MemberSession> activeSessions = [.. this.sessions.Where(session => session.IsActive)];

        if (activeSessions.Count == 0)
        {
            return Result.Failure(AuthDomainErrors.SessionNotFound);
        }

        foreach (MemberSession session in activeSessions)
        {
            session.SignOut(nowUtc);
        }

        return Result.Success();
    }

    public Result Disable(string reason, Guid disabledEventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureCanDisable();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (disabledEventId == Guid.Empty)
        {
            return Result.Failure(AuthDomainErrors.DomainEventIdRequired);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(AuthDomainErrors.DisableReasonRequired);
        }

        string trimmedReason = reason.Trim();

        if (trimmedReason.Length > DisabledReasonMaxLength)
        {
            return Result.Failure(AuthDomainErrors.DisableReasonTooLong);
        }

        this.Status = MemberStatus.Disabled;
        this.DisabledAtUtc = nowUtc;
        this.DisabledReason = trimmedReason;

        foreach (MemberSession session in this.sessions.Where(session => session.IsActive))
        {
            session.SignOut(nowUtc);
        }

        this.RaiseDomainEvent(new MemberDisabledDomainEvent(
            disabledEventId,
            nowUtc,
            this.Id,
            this.TenantId,
            trimmedReason));

        return Result.Success();
    }

    public Result Enable(Guid enabledEventId, DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureCanEnable();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (enabledEventId == Guid.Empty)
        {
            return Result.Failure(AuthDomainErrors.DomainEventIdRequired);
        }

        this.Status = MemberStatus.Active;
        this.DisabledAtUtc = null;
        this.DisabledReason = null;
        this.RaiseDomainEvent(new MemberEnabledDomainEvent(enabledEventId, nowUtc, this.Id, this.TenantId));

        return Result.Success();
    }

    public Result ResetPassword(string passwordHash)
    {
        Result statusResult = this.EnsureKnownStatus();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > PasswordHashMaxLength)
        {
            return Result.Failure(AuthDomainErrors.PasswordNotValid);
        }

        this.PasswordHash = passwordHash.Trim();
        return Result.Success();
    }

    public Result<int> RevokeSessions(Guid revokedEventId, DateTimeOffset nowUtc)
    {
        if (revokedEventId == Guid.Empty)
        {
            return Result.Failure<int>(AuthDomainErrors.DomainEventIdRequired);
        }

        List<MemberSession> activeSessions = [.. this.sessions.Where(session => session.IsActive)];

        foreach (MemberSession session in activeSessions)
        {
            session.SignOut(nowUtc);
        }

        if (activeSessions.Count > 0)
        {
            this.RaiseDomainEvent(new MemberSessionsRevokedDomainEvent(
                revokedEventId,
                nowUtc,
                this.Id,
                this.TenantId,
                activeSessions.Count));
        }

        return Result.Success(activeSessions.Count);
    }

    private Result EnsureCanAuthenticate() =>
        this.Status switch
        {
            MemberStatus.Active => Result.Success(),
            MemberStatus.Disabled => Result.Failure(AuthDomainErrors.MemberDisabled),
            _ => Result.Failure(AuthDomainErrors.MemberStatusUnknown)
        };

    private Result EnsureCanDisable() =>
        this.Status switch
        {
            MemberStatus.Active => Result.Success(),
            MemberStatus.Disabled => Result.Failure(AuthDomainErrors.MemberAlreadyDisabled),
            _ => Result.Failure(AuthDomainErrors.MemberStatusUnknown)
        };

    private Result EnsureCanEnable() =>
        this.Status switch
        {
            MemberStatus.Disabled => Result.Success(),
            MemberStatus.Active => Result.Failure(AuthDomainErrors.MemberAlreadyActive),
            _ => Result.Failure(AuthDomainErrors.MemberStatusUnknown)
        };

    private Result EnsureKnownStatus() =>
        this.Status is MemberStatus.Active or MemberStatus.Disabled
            ? Result.Success()
            : Result.Failure(AuthDomainErrors.MemberStatusUnknown);
}
