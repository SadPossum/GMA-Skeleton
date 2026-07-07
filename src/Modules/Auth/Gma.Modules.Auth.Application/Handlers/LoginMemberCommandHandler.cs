namespace Gma.Modules.Auth.Application.Handlers;

using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Errors;
using Gma.Modules.Auth.Domain.Repositories;
using Gma.Modules.Auth.Domain.Services;
using Microsoft.Extensions.Options;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class LoginMemberCommandHandler(
    IMemberRepository memberRepository,
    IPasswordHashingService passwordHashingService,
    ITokenService tokenService,
    IRefreshTokenHashingService refreshTokenHashingService,
    IOptions<AuthApplicationOptions> options,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : AuthCommandHandlerBase(tokenService, refreshTokenHashingService, clock, idGenerator),
        ICommandHandler<LoginMemberCommand, AuthTokensResponse>
{
    public async Task<Result<AuthTokensResponse>> HandleAsync(
        LoginMemberCommand command,
        CancellationToken cancellationToken)
    {
        Member? member = await memberRepository.GetByUsernameAsync(command.Username, cancellationToken).ConfigureAwait(false);

        if (member is null ||
            !member.HasActiveUsername(command.Username) ||
            !passwordHashingService.VerifyPassword(member.PasswordHash, command.Password))
        {
            return Result.Failure<AuthTokensResponse>(AuthDomainErrors.CredentialsNotValid);
        }

        var tokens = this.CreateTokens(member.Id, member.TenantId, TimeSpan.FromDays(options.Value.RefreshTokenLifetimeDays));
        Result startSessionResult = member.StartSession(tokens.SessionId, tokens.RefreshTokenHash, tokens.ExpiresAtUtc, this.Clock.UtcNow);

        if (startSessionResult.IsFailure)
        {
            return Result.Failure<AuthTokensResponse>(startSessionResult.Error);
        }

        return Result.Success(new AuthTokensResponse(tokens.AccessToken, tokens.RefreshToken));
    }
}
