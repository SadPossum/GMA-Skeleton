namespace Gma.Modules.Auth.Application.Commands;

using Gma.Modules.Auth.Contracts;
using Gma.Framework.Cqrs;

public sealed record RefreshMemberSessionCommand(string AccessToken, string RefreshToken) : ITransactionalCommand<AuthTokensResponse>;
