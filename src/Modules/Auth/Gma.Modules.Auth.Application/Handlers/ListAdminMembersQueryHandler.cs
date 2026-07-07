namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Ports;
using Gma.Modules.Auth.Application.Queries;
using Gma.Modules.Auth.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

internal sealed class ListAdminMembersQueryHandler(IAdminMemberReadRepository repository)
    : IQueryHandler<ListAdminMembersQuery, AdminMemberListResponse>
{
    public async Task<Result<AdminMemberListResponse>> HandleAsync(
        ListAdminMembersQuery query,
        CancellationToken cancellationToken)
    {
        PageRequest pageRequest = PageRequest.Normalize(query.Page, query.PageSize);

        return Result.Success(await repository.ListMembersAsync(pageRequest, cancellationToken).ConfigureAwait(false));
    }
}
