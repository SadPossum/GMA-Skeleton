namespace Gma.Modules.Auth.Tests;

using Gma.Framework.Naming;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Entities;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Events;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class MemberAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_rejects_invalid_username()
    {
        var result = CreateMember("not-an-email");
        var missing = CreateMember(null!);
        var overlong = CreateMember($"{new string('x', MemberUsername.ValueMaxLength)}@example.com");

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameNotValid, result.Error);
        Assert.True(missing.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameNotValid, missing.Error);
        Assert.True(overlong.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameNotValid, overlong.Error);
    }

    [Fact]
    public void Create_rejects_overlong_password_hash()
    {
        var result = CreateMember("member@example.com", passwordHash: new string('x', Member.PasswordHashMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.PasswordNotValid, result.Error);
    }

    [Fact]
    public void Create_raises_registered_domain_event()
    {
        var result = CreateMember("member@example.com");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.DomainEvents);
    }

    [Fact]
    public void Create_normalizes_and_rejects_invalid_tenant_id()
    {
        var normalized = CreateMember("member@example.com", " tenant-a ");
        var missing = CreateMember("member@example.com", " ");
        var invalid = CreateMember("member@example.com", new string('x', TenantIds.MaxLength + 1));

        Assert.True(normalized.IsSuccess);
        Assert.Equal("tenant-a", normalized.Value.TenantId);
        Assert.True(missing.IsFailure);
        Assert.Equal(AuthDomainErrors.TenantRequired, missing.Error);
        Assert.True(invalid.IsFailure);
        Assert.Equal(AuthDomainErrors.TenantInvalid, invalid.Error);
    }

    [Fact]
    public void Create_rejects_empty_ids_and_event_id()
    {
        var emptyMemberId = CreateMember("member@example.com", memberId: default(MemberId));
        var emptyUsernameId = CreateMember("member@example.com", usernameId: default(MemberUsernameId));
        var emptyEventId = CreateMember("member@example.com", registeredEventId: Guid.Empty);

        Assert.True(emptyMemberId.IsFailure);
        Assert.Equal(AuthDomainErrors.MemberIdRequired, emptyMemberId.Error);
        Assert.True(emptyUsernameId.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameIdRequired, emptyUsernameId.Error);
        Assert.True(emptyEventId.IsFailure);
        Assert.Equal(AuthDomainErrors.DomainEventIdRequired, emptyEventId.Error);
    }

    [Fact]
    public void Has_active_username_uses_normalized_value()
    {
        var result = CreateMember("Member@Example.com");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.HasActiveUsername("member@example.com"));
        Assert.False(result.Value.HasActiveUsername(null!));
    }

    [Fact]
    public void Add_username_validates_before_deactivating_current_username()
    {
        var member = CreateMember("member@example.com").Value;

        var result = member.AddUsername(
            new MemberUsernameId(Guid.NewGuid()),
            "not-an-email",
            MemberUsernameType.Email);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameNotValid, result.Error);
        MemberUsername username = Assert.Single(member.Usernames);
        Assert.True(username.IsActive);
        Assert.Equal("member@example.com", username.Value);
    }

    [Fact]
    public void Add_username_rejects_empty_id_before_deactivating_current_username()
    {
        var member = CreateMember("member@example.com").Value;

        var result = member.AddUsername(
            default,
            "other@example.com",
            MemberUsernameType.Email);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameIdRequired, result.Error);
        MemberUsername username = Assert.Single(member.Usernames);
        Assert.True(username.IsActive);
        Assert.Equal("member@example.com", username.Value);
    }

    [Fact]
    public void Add_username_rejects_reusing_historical_username()
    {
        var member = CreateMember("member@example.com").Value;
        var changed = member.AddUsername(
            new MemberUsernameId(Guid.NewGuid()),
            "other@example.com",
            MemberUsernameType.Email);

        var duplicate = member.AddUsername(
            new MemberUsernameId(Guid.NewGuid()),
            "MEMBER@example.com",
            MemberUsernameType.Email);

        Assert.True(changed.IsSuccess);
        Assert.True(duplicate.IsFailure);
        Assert.Equal(AuthDomainErrors.UsernameAlreadyExists, duplicate.Error);
        Assert.Equal(2, member.Usernames.Count);
        Assert.Contains(member.Usernames, username => username.Value == "member@example.com" && !username.IsActive);
        Assert.Contains(member.Usernames, username => username.Value == "other@example.com" && username.IsActive);
    }

    [Fact]
    public void Refresh_session_rotates_refresh_token_hash()
    {
        var member = CreateMember("member@example.com").Value;
        MemberSessionId sessionId = new(Guid.NewGuid());
        member.StartSession(sessionId, "refresh-hash-1", Now.AddDays(1), Now);

        var result = member.RefreshSession(
            sessionId,
            "refresh-hash-1",
            "refresh-hash-2",
            Now.AddDays(1),
            Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-hash-2", result.Value.RefreshTokenHash);
    }

    [Fact]
    public void Start_session_rejects_invalid_refresh_token_hash()
    {
        var member = CreateMember("member@example.com").Value;

        var blank = member.StartSession(new MemberSessionId(Guid.NewGuid()), " ", Now.AddDays(1), Now);
        var overlong = member.StartSession(
            new MemberSessionId(Guid.NewGuid()),
            new string('x', MemberSession.RefreshTokenHashMaxLength + 1),
            Now.AddDays(1),
            Now);

        Assert.True(blank.IsFailure);
        Assert.Equal(AuthDomainErrors.RefreshTokenHashNotValid, blank.Error);
        Assert.True(overlong.IsFailure);
        Assert.Equal(AuthDomainErrors.RefreshTokenHashNotValid, overlong.Error);
    }

    [Fact]
    public void Start_session_rejects_empty_session_id()
    {
        var member = CreateMember("member@example.com").Value;

        var result = member.StartSession(default, "refresh-hash-1", Now.AddDays(1), Now);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.SessionIdRequired, result.Error);
    }

    [Fact]
    public void Refresh_session_rejects_invalid_new_refresh_token_hash()
    {
        var member = CreateMember("member@example.com").Value;
        MemberSessionId sessionId = new(Guid.NewGuid());
        member.StartSession(sessionId, "refresh-hash-1", Now.AddDays(1), Now);

        var result = member.RefreshSession(
            sessionId,
            "refresh-hash-1",
            new string('x', MemberSession.RefreshTokenHashMaxLength + 1),
            Now.AddDays(1),
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.RefreshTokenHashNotValid, result.Error);
    }

    [Fact]
    public void Disable_revokes_active_sessions_and_blocks_new_sessions()
    {
        var member = CreateMember("member@example.com").Value;
        member.StartSession(new MemberSessionId(Guid.NewGuid()), "refresh-hash-1", Now.AddDays(1), Now);

        var disableResult = member.Disable("support request", Guid.NewGuid(), Now);
        var startResult = member.StartSession(new MemberSessionId(Guid.NewGuid()), "refresh-hash-2", Now.AddDays(1), Now);

        Assert.True(disableResult.IsSuccess);
        Assert.All(member.Sessions, session => Assert.False(session.IsActive));
        Assert.True(startResult.IsFailure);
        Assert.Equal(AuthDomainErrors.MemberDisabled, startResult.Error);
    }

    [Fact]
    public void Disable_requires_reason()
    {
        var member = CreateMember("member@example.com").Value;

        var blankResult = member.Disable(" ", Guid.NewGuid(), Now);
        var nullResult = member.Disable(null!, Guid.NewGuid(), Now);

        Assert.True(blankResult.IsFailure);
        Assert.Equal(AuthDomainErrors.DisableReasonRequired, blankResult.Error);
        Assert.True(nullResult.IsFailure);
        Assert.Equal(AuthDomainErrors.DisableReasonRequired, nullResult.Error);
    }

    [Fact]
    public void Disable_enable_and_revoke_sessions_reject_empty_event_id()
    {
        var member = CreateMember("member@example.com").Value;
        member.StartSession(new MemberSessionId(Guid.NewGuid()), "refresh-hash-1", Now.AddDays(1), Now);

        Result disableResult = member.Disable("support request", Guid.Empty, Now);
        Result<int> revokeResult = member.RevokeSessions(Guid.Empty, Now);

        Assert.True(disableResult.IsFailure);
        Assert.Equal(AuthDomainErrors.DomainEventIdRequired, disableResult.Error);
        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.True(revokeResult.IsFailure);
        Assert.Equal(AuthDomainErrors.DomainEventIdRequired, revokeResult.Error);
        Assert.All(member.Sessions, session => Assert.True(session.IsActive));

        Result validDisable = member.Disable("support request", Guid.NewGuid(), Now);
        Result enableResult = member.Enable(Guid.Empty, Now.AddMinutes(1));

        Assert.True(validDisable.IsSuccess);
        Assert.True(enableResult.IsFailure);
        Assert.Equal(AuthDomainErrors.DomainEventIdRequired, enableResult.Error);
        Assert.Equal(MemberStatus.Disabled, member.Status);
    }

    [Fact]
    public void Disable_rejects_overlong_reason()
    {
        var member = CreateMember("member@example.com").Value;
        string reason = new('x', Member.DisabledReasonMaxLength + 1);

        var result = member.Disable(reason, Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.DisableReasonTooLong, result.Error);
    }

    [Fact]
    public void Disable_trims_reason_for_state_and_event()
    {
        var member = CreateMember("member@example.com").Value;

        var result = member.Disable(" support request ", Guid.NewGuid(), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("support request", member.DisabledReason);
        Assert.Equal(
            "support request",
            member.DomainEvents.OfType<MemberDisabledDomainEvent>().Single().Reason);
    }

    [Fact]
    public void Enable_reactivates_disabled_member()
    {
        var member = CreateMember("member@example.com").Value;
        member.Disable("support request", Guid.NewGuid(), Now);

        var result = member.Enable(Guid.NewGuid(), Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(MemberStatus.Active, member.Status);
        Assert.Null(member.DisabledReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Authentication_and_security_mutations_reject_unknown_member_status(int statusValue)
    {
        var member = CreateMember("member@example.com").Value;
        MemberSessionId sessionId = new(Guid.NewGuid());
        Result<MemberSession> session = member.StartSession(sessionId, "refresh-hash-1", Now.AddDays(1), Now);
        Assert.True(session.IsSuccess);
        member.ClearDomainEvents();
        SetStatus(member, (MemberStatus)statusValue);

        Result<MemberSession> startSession = member.StartSession(
            new MemberSessionId(Guid.NewGuid()),
            "refresh-hash-2",
            Now.AddDays(1),
            Now);
        Result<MemberSession> refreshSession = member.RefreshSession(
            sessionId,
            "refresh-hash-1",
            "refresh-hash-2",
            Now.AddDays(1),
            Now);
        Result disable = member.Disable("support request", Guid.NewGuid(), Now);
        Result enable = member.Enable(Guid.NewGuid(), Now);
        Result resetPassword = member.ResetPassword("new-hash");

        Assert.Equal(AuthDomainErrors.MemberStatusUnknown, startSession.Error);
        Assert.Equal(AuthDomainErrors.MemberStatusUnknown, refreshSession.Error);
        Assert.Equal(AuthDomainErrors.MemberStatusUnknown, disable.Error);
        Assert.Equal(AuthDomainErrors.MemberStatusUnknown, enable.Error);
        Assert.Equal(AuthDomainErrors.MemberStatusUnknown, resetPassword.Error);
        Assert.Equal((MemberStatus)statusValue, member.Status);
        Assert.Empty(member.DomainEvents);
    }

    [Fact]
    public void Reset_password_rejects_overlong_hash()
    {
        var member = CreateMember("member@example.com").Value;

        var result = member.ResetPassword(new string('x', Member.PasswordHashMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(AuthDomainErrors.PasswordNotValid, result.Error);
    }

    private static Gma.Framework.Results.Result<Member> CreateMember(
        string username,
        string tenantId = "tenant-a",
        string passwordHash = "hash",
        MemberId? memberId = null,
        MemberUsernameId? usernameId = null,
        Guid? registeredEventId = null) =>
        Member.Create(
            memberId ?? new MemberId(Guid.NewGuid()),
            tenantId,
            username,
            MemberUsernameType.Email,
            passwordHash,
            usernameId ?? new MemberUsernameId(Guid.NewGuid()),
            registeredEventId ?? Guid.NewGuid(),
            Now);

    private static void SetStatus(Member member, MemberStatus status) =>
        typeof(Member)
            .GetProperty(nameof(Member.Status))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(member, [status]);
}
