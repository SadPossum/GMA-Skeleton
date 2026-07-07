namespace Gma.Modules.Auth.Application.Commands;

using Gma.Modules.Auth.Contracts;
using Gma.Framework.Cqrs;

public sealed record AdminCreateMemberCommand(string Username, UsernameType UsernameType, string Password)
    : ITransactionalCommand<AdminCreatedMemberResponse>;
