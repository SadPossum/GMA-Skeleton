namespace Gma.Modules.Auth.Application.Ports;

using Gma.Modules.Auth.Contracts;
using Gma.Framework.Pagination;

public interface IAdminMemberReadRepository
{
    Task<AdminMemberListResponse> ListMembersAsync(PageRequest pageRequest, CancellationToken cancellationToken);
    Task<AdminMemberDetails?> GetMemberAsync(Guid memberId, CancellationToken cancellationToken);
}
