namespace Gma.Modules.Auth.Contracts;

public sealed record AdminMemberListResponse(
    IReadOnlyList<AdminMemberListItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
