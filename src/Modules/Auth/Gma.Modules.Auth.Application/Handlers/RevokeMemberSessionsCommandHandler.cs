namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Repositories;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class RevokeMemberSessionsCommandHandler(
    IMemberRepository memberRepository,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<RevokeMemberSessionsCommand, AdminRevokeSessionsResponse>
{
    public async Task<Result<AdminRevokeSessionsResponse>> HandleAsync(
        RevokeMemberSessionsCommand command,
        CancellationToken cancellationToken)
    {
        Member? member = await memberRepository.GetByIdAsync(new MemberId(command.MemberId), cancellationToken).ConfigureAwait(false);

        if (member is null)
        {
            return Result.Failure<AdminRevokeSessionsResponse>(AuthDomainErrors.MemberNotFound);
        }

        Result<int> result = member.RevokeSessions(idGenerator.NewId(), clock.UtcNow);

        return result.IsSuccess
            ? Result.Success(new AdminRevokeSessionsResponse(result.Value))
            : Result.Failure<AdminRevokeSessionsResponse>(result.Error);
    }
}
