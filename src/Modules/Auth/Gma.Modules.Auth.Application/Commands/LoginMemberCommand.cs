namespace Gma.Modules.Auth.Application.Commands;

using Gma.Modules.Auth.Contracts;
using Gma.Framework.Cqrs;

public sealed record LoginMemberCommand(string Username, string Password) : ITransactionalCommand<AuthTokensResponse>;
