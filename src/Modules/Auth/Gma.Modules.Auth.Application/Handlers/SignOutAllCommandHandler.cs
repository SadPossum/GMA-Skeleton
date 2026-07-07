namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Repositories;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class SignOutAllCommandHandler(IMemberRepository memberRepository, ISystemClock clock)
    : ICommandHandler<SignOutAllCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(SignOutAllCommand command, CancellationToken cancellationToken)
    {
        Member? member = await memberRepository.GetByIdAsync(new MemberId(command.MemberId), cancellationToken)
            .ConfigureAwait(false);

        if (member is null)
        {
            return Result.Failure<Unit>(AuthDomainErrors.MemberNotFound);
        }

        Result result = member.SignOutAll(clock.UtcNow);

        return result.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(result.Error);
    }
}
