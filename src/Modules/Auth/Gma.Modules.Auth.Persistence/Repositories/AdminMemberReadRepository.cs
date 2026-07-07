namespace Gma.Modules.Auth.Persistence.Repositories;

using Gma.Modules.Auth.Application.Ports;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Pagination;
using ContractMemberStatus = Gma.Modules.Auth.Contracts.MemberStatus;
using DomainMemberStatus = Gma.Modules.Auth.Domain.Enums.MemberStatus;

internal sealed class AdminMemberReadRepository(AuthDbContext dbContext) : IAdminMemberReadRepository
{
    public async Task<AdminMemberListResponse> ListMembersAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<Member> query = dbContext.Members
            .AsNoTracking()
            .Include(member => member.Usernames)
            .Include(member => member.Sessions)
            .AsSplitQuery()
            .OrderBy(member => member.RegisteredAtUtc);

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        Member[] members = await query
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        AdminMemberListItem[] items = members
            .Select(member => new AdminMemberListItem(
                member.Id.Value,
                member.TenantId,
                ToContractStatus(member.Status),
                GetActiveUsername(member),
                member.RegisteredAtUtc,
                member.Sessions.Count(session => session.IsActive)))
            .ToArray();

        return new AdminMemberListResponse(items, pageRequest.Page, pageRequest.PageSize, totalCount);
    }

    public async Task<AdminMemberDetails?> GetMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        Member? member = await dbContext.Members
            .AsNoTracking()
            .Include(item => item.Usernames)
            .Include(item => item.Sessions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == new MemberId(memberId), cancellationToken)
            .ConfigureAwait(false);

        return member is null
            ? null
            : new AdminMemberDetails(
                member.Id.Value,
                member.TenantId,
                ToContractStatus(member.Status),
                GetActiveUsername(member),
                member.RegisteredAtUtc,
                member.DisabledAtUtc,
                member.DisabledReason,
                member.Sessions.Count(session => session.IsActive),
                member.Sessions.Count);
    }

    private static string? GetActiveUsername(Member member) =>
        member.Usernames
            .Where(username => username.IsActive)
            .OrderBy(username => username.UsernameType)
            .Select(username => username.Value)
            .FirstOrDefault();

    private static ContractMemberStatus ToContractStatus(DomainMemberStatus status) =>
        status switch
        {
            DomainMemberStatus.Active => ContractMemberStatus.Active,
            DomainMemberStatus.Disabled => ContractMemberStatus.Disabled,
            _ => ContractMemberStatus.Unknown
        };
}
