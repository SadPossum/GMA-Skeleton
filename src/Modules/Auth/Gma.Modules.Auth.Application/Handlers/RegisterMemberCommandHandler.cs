namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Repositories;
using Gma.Modules.Auth.Domain.Services;
using Gma.Modules.Auth.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tenancy;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class RegisterMemberCommandHandler(
    IMemberRepository memberRepository,
    ITenantContext tenantContext,
    IPasswordHashingService passwordHashingService,
    ITokenService tokenService,
    IRefreshTokenHashingService refreshTokenHashingService,
    IOptions<AuthApplicationOptions> options,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : AuthCommandHandlerBase(tokenService, refreshTokenHashingService, clock, idGenerator),
        ICommandHandler<RegisterMemberCommand, AuthTokensResponse>
{
    public async Task<Result<AuthTokensResponse>> HandleAsync(
        RegisterMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return Result.Failure<AuthTokensResponse>(AuthApplicationErrors.TenantRequired);
        }

        Result<MemberUsernameType> usernameType = UsernameTypeMapper.Map(command.UsernameType);
        if (usernameType.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(usernameType.Error);
        }

        string passwordHash = passwordHashingService.HashPassword(command.Password);
        Result<Member> memberResult = Member.Create(
            new MemberId(this.IdGenerator.NewId()),
            tenantContext.TenantId,
            command.Username,
            usernameType.Value,
            passwordHash,
            new MemberUsernameId(this.IdGenerator.NewId()),
            this.IdGenerator.NewId(),
            this.Clock.UtcNow);

        if (memberResult.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(memberResult.Error);
        }

        if (await memberRepository.UsernameExistsAsync(command.Username, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<AuthTokensResponse>(AuthDomainErrors.UsernameAlreadyExists);
        }

        Member member = memberResult.Value;
        var tokens = this.CreateTokens(member.Id, member.TenantId, TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays));
        Result startSessionResult = member.StartSession(tokens.SessionId, tokens.RefreshTokenHash, tokens.ExpiresAtUtc, this.Clock.UtcNow);

        if (startSessionResult.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(startSessionResult.Error);
        }

        await memberRepository.AddAsync(member, cancellationToken).ConfigureAwait(false);

        return Result.Success(new AuthTokensResponse(tokens.AccessToken, tokens.RefreshToken));
    }
}
