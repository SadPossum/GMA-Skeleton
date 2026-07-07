namespace Gma.Modules.Auth.Contracts;

public sealed record AdminMemberListItem(
    Guid MemberId,
    string TenantId,
    MemberStatus Status,
    string? ActiveUsername,
    DateTimeOffset RegisteredAtUtc,
    int ActiveSessionCount);
