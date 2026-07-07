namespace Gma.Modules.Auth.Tests;

using Gma.Framework.Naming;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AuthIdValueObjectTests
{
    [Fact]
    public void Member_id_requires_non_empty_value()
    {
        Guid value = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal(value, new MemberId(value).Value);
        Assert.Throws<ArgumentException>(() => new MemberId(Guid.Empty));
    }

    [Fact]
    public void Member_username_id_requires_non_empty_value()
    {
        Guid value = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal(value, new MemberUsernameId(value).Value);
        Assert.Throws<ArgumentException>(() => new MemberUsernameId(Guid.Empty));
    }

    [Fact]
    public void Member_session_id_requires_non_empty_value()
    {
        Guid value = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal(value, new MemberSessionId(value).Value);
        Assert.Throws<ArgumentException>(() => new MemberSessionId(Guid.Empty));
    }

    [Fact]
    public void Default_struct_values_remain_empty_for_aggregate_defensive_checks()
    {
        Assert.Equal(Guid.Empty, default(MemberId).Value);
        Assert.Equal(Guid.Empty, default(MemberUsernameId).Value);
        Assert.Equal(Guid.Empty, default(MemberSessionId).Value);
    }

    [Fact]
    public void Access_token_claims_normalize_and_validate_identity()
    {
        MemberId memberId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        MemberSessionId sessionId = new(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"));

        AccessTokenClaims claims = new(memberId, " tenant-a ", sessionId);

        Assert.Equal(memberId, claims.MemberId);
        Assert.Equal("tenant-a", claims.TenantId);
        Assert.Equal(sessionId, claims.SessionId);
        Assert.Throws<ArgumentException>(() => new AccessTokenClaims(default, "tenant-a", sessionId));
        Assert.Throws<ArgumentException>(() => new AccessTokenClaims(memberId, "tenant-a", default));
        Assert.Throws<ArgumentException>(() => new AccessTokenClaims(memberId, " ", sessionId));
        Assert.Throws<ArgumentException>(() => new AccessTokenClaims(memberId, new string('x', TenantIds.MaxLength + 1), sessionId));
    }
}
