namespace Gma.Modules.Administration.Application.Handlers;

using Gma.Modules.Administration.Application.Ports;
using Gma.Modules.Administration.Application.Queries;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ListRolesQueryHandler(IAdminRbacRepository repository)
    : IQueryHandler<ListRolesQuery, IReadOnlyList<AdminRoleDetails>>
{
    public async Task<Result<IReadOnlyList<AdminRoleDetails>>> HandleAsync(
        ListRolesQuery query,
        CancellationToken cancellationToken) =>
        Result.Success(await repository.ListRolesAsync(cancellationToken).ConfigureAwait(false));
}
