namespace Gma.Modules.Auth.Contracts;

public sealed record AdminMemberDetails(
    Guid MemberId,
    string TenantId,
    MemberStatus Status,
    string? ActiveUsername,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? DisabledAtUtc,
    string? DisabledReason,
    int ActiveSessionCount,
    int TotalSessionCount);
