namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Ports;
using Gma.Modules.Auth.Application.Queries;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetAdminMemberQueryHandler(IAdminMemberReadRepository repository)
    : IQueryHandler<GetAdminMemberQuery, AdminMemberDetails>
{
    public async Task<Result<AdminMemberDetails>> HandleAsync(
        GetAdminMemberQuery query,
        CancellationToken cancellationToken)
    {
        AdminMemberDetails? member = await repository.GetMemberAsync(query.MemberId, cancellationToken).ConfigureAwait(false);

        return member is null
            ? Result.Failure<AdminMemberDetails>(AuthDomainErrors.MemberNotFound)
            : Result.Success(member);
    }
}
