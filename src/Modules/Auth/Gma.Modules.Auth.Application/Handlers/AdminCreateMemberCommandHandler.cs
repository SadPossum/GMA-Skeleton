namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Repositories;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tenancy;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class AdminCreateMemberCommandHandler(
    IMemberRepository memberRepository,
    ITenantContext tenantContext,
    IPasswordHashingService passwordHashingService,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<AdminCreateMemberCommand, AdminCreatedMemberResponse>
{
    public async Task<Result<AdminCreatedMemberResponse>> HandleAsync(
        AdminCreateMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return Result.Failure<AdminCreatedMemberResponse>(AuthApplicationErrors.TenantRequired);
        }

        Result<MemberUsernameType> usernameType = UsernameTypeMapper.Map(command.UsernameType);
        if (usernameType.IsFailure)
        {
            return Result.Failure<AdminCreatedMemberResponse>(usernameType.Error);
        }

        Result<Member> memberResult = Member.Create(
            new MemberId(idGenerator.NewId()),
            tenantContext.TenantId,
            command.Username,
            usernameType.Value,
            passwordHashingService.HashPassword(command.Password),
            new MemberUsernameId(idGenerator.NewId()),
            idGenerator.NewId(),
            clock.UtcNow);

        if (memberResult.IsFailure)
        {
            return Result.Failure<AdminCreatedMemberResponse>(memberResult.Error);
        }

        if (await memberRepository.UsernameExistsAsync(command.Username, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<AdminCreatedMemberResponse>(AuthDomainErrors.UsernameAlreadyExists);
        }

        Member member = memberResult.Value;
        await memberRepository.AddAsync(member, cancellationToken).ConfigureAwait(false);

        return Result.Success(new AdminCreatedMemberResponse(member.Id.Value, command.Username));
    }
}
